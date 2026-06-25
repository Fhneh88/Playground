using System.Net.Mail;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryLib;

// ─── Интерфейсы ───────────────────────────────────────────────────────────────

public interface IUserValidator
{
    void Validate(string name, string email, string password);
}

public interface IPasswordHasher
{
    string Hash(string password);
}

public interface IUserRepository
{
    void Save(string name, string email, string hashedPassword);
}

public interface IEmailSender
{
    void Send(string toEmail, string subject, string body);
}

public interface ILogger
{
    void Log(string message);
}

// ─── Реализации ───────────────────────────────────────────────────────────────

public class UserValidator : IUserValidator
{
    public void Validate(string name, string email, string password)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Name is required");
        if (!email.Contains('@'))
            throw new ArgumentException("Invalid email");
        if (password.Length < 6)
            throw new ArgumentException("Password too short");
    }
}

public class Base64PasswordHasher : IPasswordHasher
{
    public string Hash(string password) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));
}

public class FileUserRepository(string filePath) : IUserRepository
{
    public void Save(string name, string email, string hashedPassword) =>
        File.AppendAllText(filePath, $"{name},{email},{hashedPassword}\n");
}

public class SmtpEmailSender(string host, string from) : IEmailSender
{
    public void Send(string toEmail, string subject, string body)
    {
        var smtp = new SmtpClient(host);
        smtp.Send(from, toEmail, subject, body);
    }
}

public class FileLogger(string filePath) : ILogger
{
    public void Log(string message) =>
        File.AppendAllText(filePath, $"{DateTime.Now}: {message}\n");
}

// ─── UserManager ──────────────────────────────────────────────────────────────

public class UserManager(
    IUserValidator validator,
    IPasswordHasher hasher,
    IUserRepository repository,
    IEmailSender emailSender,
    ILogger logger)
{
    public void RegisterUser(string name, string email, string password)
    {
        validator.Validate(name, email, password);

        var hashedPassword = hasher.Hash(password);

        repository.Save(name, email, hashedPassword);

        emailSender.Send(email, "Welcome!", $"Hello {name}!");

        logger.Log($"User {email} registered");
    }
}