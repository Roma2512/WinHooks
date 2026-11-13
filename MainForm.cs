using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using KeyboardHookLab.Interop;

namespace KeyboardHookLab;

public sealed class MainForm : Form
{
    private readonly TextBox _logTextBox;
    private readonly CheckBox _hookCheckBox;
    private readonly CheckBox _onlyFunctionKeysCheckBox;
    private readonly Label _statusLabel;
    private readonly Label _counterLabel;
    private readonly KeyboardHook _keyboardHook;

    // Счётчик всех записанных событий.
    private int _eventCount = 0;

    // Чтобы не включать hook, пока форма уже закрывается.
    private bool _isClosing = false;

    public MainForm()
    {
        Text = "Keyboard Hook Laboratory";
        Width = 900;
        Height = 520;
        StartPosition = FormStartPosition.CenterScreen;

        KeyPreview = true;

        _keyboardHook = new KeyboardHook();
        _keyboardHook.KeyPressed += OnHookKeyPressed;

        _logTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical
        };

        _hookCheckBox = new CheckBox
        {
            Text = "Включить low-level hook",
            AutoSize = true,
            Margin = new Padding(8)
        };

        _onlyFunctionKeysCheckBox = new CheckBox
        {
            Text = "Только F1-F12",
            AutoSize = true,
            Margin = new Padding(8)
        };

        var clearButton = new Button
        {
            Text = "Очистить журнал",
            AutoSize = true
        };

        _statusLabel = new Label
        {
            Text = "Hook не установлен",
            AutoSize = true,
            Margin = new Padding(8)
        };

        _counterLabel = new Label
        {
            Text = "Событий: 0",
            AutoSize = true,
            Margin = new Padding(8)
        };

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false
        };

        toolbar.Controls.Add(_hookCheckBox);
        toolbar.Controls.Add(_onlyFunctionKeysCheckBox);
        toolbar.Controls.Add(clearButton);
        toolbar.Controls.Add(_statusLabel);
        toolbar.Controls.Add(_counterLabel);

        Controls.Add(_logTextBox);
        Controls.Add(toolbar);

        // Обычные события WinForms.
        KeyDown += OnLocalKeyDown;
        KeyUp += OnLocalKeyUp;

        clearButton.Click += (_, _) => ClearLog();

        _hookCheckBox.CheckedChanged += OnHookCheckedChanged;

        Activated += OnFormActivated;
        Deactivate += OnFormDeactivated;
        FormClosing += OnFormClosing;
    }

    // -------------------- Вспомогательное --------------------

    private void ClearLog()
    {
        _logTextBox.Clear();
        _eventCount = 0;
        _counterLabel.Text = "Событий: 0";
    }

    private bool IsFunctionKey(Keys key)
    {
        return key >= Keys.F1 && key <= Keys.F12;
    }

    private void AppendLog(string source, string key)
    {
        string timestamp = DateTimeOffset.Now.ToString("HH:mm:ss.fff");

        _logTextBox.AppendText(
            $"[{timestamp}] [{source}] {key}" + Environment.NewLine);

        _eventCount++;
        _counterLabel.Text = $"Событий: {_eventCount}";
    }

    // -------------------- WinForms-события --------------------

    private void OnLocalKeyDown(object? sender, KeyEventArgs e)
    {
        // Задание: Escape очищает журнал.
        if (e.KeyCode == Keys.Escape)
        {
            ClearLog();
            return;
        }

        // Бонусное задание: фильтр только F1-F12.
        if (_onlyFunctionKeysCheckBox.Checked && !IsFunctionKey(e.KeyCode))
            return;

        AppendLog("WinForms KeyDown", e.KeyCode.ToString());
    }

    private void OnLocalKeyUp(object? sender, KeyEventArgs e)
    {
        if (_onlyFunctionKeysCheckBox.Checked && !IsFunctionKey(e.KeyCode))
            return;

        AppendLog("WinForms KeyUp", e.KeyCode.ToString());
    }

    // -------------------- События от hook --------------------

    private void OnHookKeyPressed(Keys key)
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        try
        {
            // Обновляем UI через очередь сообщений формы.
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed)
                    return;

                if (_onlyFunctionKeysCheckBox.Checked && !IsFunctionKey(key))
                    return;

                AppendLog("Low-level hook", key.ToString());
            }));
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"Не удалось обновить журнал: {ex}");
        }
    }

    // -------------------- Управление hook --------------------

    private void OnHookCheckedChanged(object? sender, EventArgs e)
    {
        if (_isClosing)
            return;

        if (_hookCheckBox.Checked)
        {
            if (Form.ActiveForm == this)
                StartHook();
        }
        else
        {
            StopHook();
        }
    }

    private void OnFormActivated(object? sender, EventArgs e)
    {
        if (_hookCheckBox.Checked)
            StartHook();
    }

    private void OnFormDeactivated(object? sender, EventArgs e)
    {
        StopHook();
    }

    private void StartHook()
    {
        if (_keyboardHook.IsInstalled)
            return;

        try
        {
            _keyboardHook.Start();
            _statusLabel.Text = "Hook установлен";
            AppendLog("System", "Hook установлен");
        }
        catch (Win32Exception ex)
        {
            _statusLabel.Text = "Ошибка установки hook";
            AppendLog("Error", ex.Message);
            _hookCheckBox.Checked = false;
        }
    }

    private void StopHook()
    {
        if (!_keyboardHook.IsInstalled)
            return;

        try
        {
            _keyboardHook.Stop();
            _statusLabel.Text = "Hook не установлен";
            AppendLog("System", "Hook снят");
        }
        catch (Win32Exception ex)
        {
            _statusLabel.Text = "Ошибка снятия hook";
            AppendLog("Error", ex.Message);
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // Задание: кнопка включения hook недоступна при закрытии.
        _isClosing = true;
        _hookCheckBox.Enabled = false;
        _onlyFunctionKeysCheckBox.Enabled = false;

        StopHook();

        try
        {
            _keyboardHook.Dispose();
        }
        catch (Win32Exception ex)
        {
            Debug.WriteLine($"Ошибка освобождения hook: {ex}");
        }
    }
}