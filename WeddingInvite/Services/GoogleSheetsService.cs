using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Options;
using WeddingInvite.Models;

namespace WeddingInvite.Services;

public class GoogleSheetsService : IGoogleSheetsService
{
    private readonly SheetsOptions _options;

    public GoogleSheetsService(IOptions<SheetsOptions> options)
    {
        _options = options.Value;
    }

    public async Task AppendRowAsync(RsvpModel rsvp)
    {
        var credential = GoogleCredential
            .FromFile(_options.CredentialsPath)
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        var service = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = _options.ApplicationName
        });

        var row = new List<object>
        {
            DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            rsvp.GuestName,
            rsvp.Starter,
            rsvp.MainCourse,
            rsvp.Dessert,
            string.Join(", ", rsvp.Drinks)
        };

        var body = new ValueRange
        {
            Values = new List<IList<object>> { row }
        };

        var request = service.Spreadsheets.Values.Append(
            body, _options.SpreadsheetId, "Sheet1");
        request.ValueInputOption =
            SpreadsheetsResource.ValuesResource.AppendRequest
                .ValueInputOptionEnum.USERENTERED;

        await request.ExecuteAsync();
    }
}
