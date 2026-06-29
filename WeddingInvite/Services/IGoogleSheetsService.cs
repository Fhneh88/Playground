using System.Threading.Tasks;
using WeddingInvite.Models;

namespace WeddingInvite.Services;

public interface IGoogleSheetsService
{
    Task AppendRowAsync(RsvpModel rsvp);
}
