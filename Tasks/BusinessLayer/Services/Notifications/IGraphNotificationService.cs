using Academikus.AgenteInteligenteMentoresTareas.Entity.Models;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Services.GraphNotification;

public interface IGraphNotificationService
{
    Task ProcessNotificationAsync(NotificationValue notification);
}
