using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.VirtualMentorDB.Entities;
namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Services.Users;

public interface IUserService
{
    Task SyncUsersFromExcelAsync();
}
