namespace WeddingInvite.Services;

public class SheetsOptions
{
    public string SpreadsheetId { get; set; } = string.Empty;
    public string CredentialsPath { get; set; } = "credentials.json";
    public string ApplicationName { get; set; } = "WeddingInvite";
}
