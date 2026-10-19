using System;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;

namespace WpfLoginApp;

public partial class MainWindow : Window
{
    private int _failedAttempts;
    private DateTime _lockoutUntil;
    private bool _captchaRequired;
    private int _captchaAnswer;
    private readonly DispatcherTimer _lockTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    private static readonly string ConnectionString =
        ConfigurationManager.ConnectionStrings["LoginDb"]?.ConnectionString
        ?? "Server=.\\SQLEXPRESS;Database=LoginDb2;Trusted_Connection=True;TrustServerCertificate=True;";

    public MainWindow()
    {
        InitializeComponent();
        cbRole.SelectedIndex = 0;
        _lockTimer.Tick += LockTimer_Tick;
    }

    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        if (DateTime.Now < _lockoutUntil)
        {
            ShowStatus($"Аккаунт заблокирован на 1 минуту. Осталось: {(_lockoutUntil - DateTime.Now).Seconds} сек.", Brushes.Red);
            btnLogin.IsEnabled = false;
            return;
        }

        btnLogin.IsEnabled = true;
        txtLockout.Text = string.Empty;

        if (_captchaRequired)
        {
            if (!ValidateCaptcha())
            {
                return;
            }

            _captchaRequired = false;
            pnlCaptcha.Visibility = Visibility.Collapsed;
        }

        string login = txtLogin.Text.Trim();
        string password = txtPassword.Password;

        if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
        {
            ShowStatus("Введите логин и пароль.", Brushes.Red);
            return;
        }

        if (TryValidateUser(login, password, out string? roleName))
        {
            _failedAttempts = 0;
            _captchaRequired = false;
            pnlCaptcha.Visibility = Visibility.Collapsed;
            ShowStatus($"Успешный вход! Роль: {roleName}", Brushes.Green);
            MessageBox.Show($"Добро пожаловать! Роль: {roleName}", "Авторизация",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            _failedAttempts++;
            ShowStatus($"Неверный логин или пароль. Попытка {_failedAttempts} из 3.", Brushes.Red);
            GenerateCaptcha();
            _captchaRequired = true;
            pnlCaptcha.Visibility = Visibility.Visible;
            txtCaptchaAnswer.Text = string.Empty;
            txtCaptchaAnswer.Focus();

            if (_failedAttempts >= 3)
            {
                _lockoutUntil = DateTime.Now.AddMinutes(1);
                btnLogin.IsEnabled = false;
                _lockTimer.Start();
                UpdateLockoutText();
                ShowStatus("Превышено количество попыток. Кнопка заблокирована на 1 минуту.", Brushes.Red);
            }
        }
    }

    private bool ValidateCaptcha()
    {
        if (!int.TryParse(txtCaptchaAnswer.Text.Trim(), out int answer))
        {
            ShowStatus("Введите число для проверки капчи.", Brushes.Red);
            GenerateCaptcha();
            txtCaptchaAnswer.Text = string.Empty;
            txtCaptchaAnswer.Focus();
            return false;
        }

        if (answer != _captchaAnswer)
        {
            ShowStatus("Неверный ответ на капчу. Попробуйте снова.", Brushes.Red);
            GenerateCaptcha();
            txtCaptchaAnswer.Text = string.Empty;
            txtCaptchaAnswer.Focus();
            return false;
        }

        ShowStatus("Капча верна. Проверка учетных данных...", Brushes.Green);
        return true;
    }

    private bool TryValidateUser(string login, string password, out string? roleName)
    {
        roleName = null;

        using var connection = new SqlConnection(ConnectionString);
        using var command = new SqlCommand(
            @"SELECT r.Name FROM dbo.Users u
              JOIN dbo.Roles r ON u.RoleId = r.Id
              WHERE u.Login = @Login AND u.Password = @Password", connection);

        command.Parameters.Add("@Login", SqlDbType.NVarChar).Value = login;
        command.Parameters.Add("@Password", SqlDbType.NVarChar).Value = password;

        try
        {
            connection.Open();
            var result = command.ExecuteScalar();
            if (result != null)
            {
                roleName = result.ToString();
                return true;
            }
            return false;
        }
        catch (SqlException ex)
        {
            MessageBox.Show($"Ошибка подключения к базе данных: {ex.Message}", "Ошибка БД",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void GenerateCaptcha()
    {
        Random rnd = new();
        int a = rnd.Next(1, 21);
        int b = rnd.Next(1, 21);
        _captchaAnswer = a + b;
        txtCaptcha.Text = $"{a} + {b} = ?";
    }

    private void ShowStatus(string text, Brush color)
    {
        txtStatus.Text = text;
        txtStatus.Foreground = color;
    }

    private void LockTimer_Tick(object sender, EventArgs e)
    {
        UpdateLockoutText();

        if (DateTime.Now >= _lockoutUntil)
        {
            _lockTimer.Stop();
            btnLogin.IsEnabled = true;
            txtLockout.Text = string.Empty;
            ShowStatus("Блокировка снята. Повторите попытку входа.", Brushes.Green);
        }
    }

    private void UpdateLockoutText()
    {
        int remaining = (int)Math.Ceiling((_lockoutUntil - DateTime.Now).TotalSeconds);
        txtLockout.Text = remaining > 0
            ? $"Ожидание разблокировки: {remaining} сек."
            : string.Empty;
    }
}
