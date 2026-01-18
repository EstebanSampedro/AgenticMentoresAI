using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Academikus.AgenteInteligenteMentoresTareas.Business.Hubs;

/// <summary>
/// Hub de SignalR para comunicación en tiempo real entre estudiantes,
/// mentores e IA dentro de los chats.
/// </summary>
public class ChatHub : Hub
{
    private const string MentorGroupPrefix = "mentor:";
    private const string ChatGroupPrefix = "chat:";

    /// <summary>
    /// Evento ejecutado cuando un cliente realiza la conexión al hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        Console.WriteLine("[Tasks] Conexión a SignalR");

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Evento ejecutado cuando un cliente desconecta del hub.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            Console.WriteLine($"[ChatHub][ERROR] {exception.Message}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Agrega al cliente al grupo estable del mentor.
    /// Este grupo debe unirse siempre al iniciar la app para recibir eventos como "ChatIdAssigned".
    /// </summary>
    /// <param name="mentorEntraId">EntraUserId del mentor.</param>
    [Authorize]
    public async Task JoinMentorGroup(string mentorEntraId)
    {
        if (string.IsNullOrWhiteSpace(mentorEntraId))
        {
            Console.WriteLine("[ChatHub] mentorEntraId inválido en JoinMentorGroup.");
            return;
        }

        var groupName = $"{MentorGroupPrefix}{mentorEntraId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        Console.WriteLine($"[ChatHub] Conectado a grupo mentor: {groupName}");
    }

    ///// <summary>
    ///// Agrega al usuario a un grupo de chat identificado por su chatId.
    ///// </summary>
    ///// <param name="chatId">Identificador del chat al que el usuario se añadirá.</param>
    //[Authorize]
    //public async Task AddToGroup(string chatId)
    //{
    //    if (string.IsNullOrWhiteSpace(chatId))
    //    {
    //        Console.WriteLine("[ChatHub] chatId inválido en AddToGroup.");
    //    }
    //    else
    //    {
    //        await Groups.AddToGroupAsync(Context.ConnectionId, chatId);
    //        Console.WriteLine($"ChatId recibido: {chatId}");
    //    }
    //}

    /// <summary>
    /// Agrega al cliente al grupo de un chat (basado en MSTeamsChatId).
    /// Se usa para recibir mensajes en vivo del chat.
    /// </summary>
    /// <param name="msTeamsChatId">Id del chat de Microsoft Teams.</param>
    [Authorize]
    public async Task JoinChatGroup(string msTeamsChatId)
    {
        if (string.IsNullOrWhiteSpace(msTeamsChatId))
        {
            Console.WriteLine("[ChatHub] msTeamsChatId inválido en JoinChatGroup.");
            return;
        }

        var groupName = $"{ChatGroupPrefix}{msTeamsChatId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        Console.WriteLine($"[ChatHub] Conectado a grupo chat: {groupName}");
    }

    ///// <summary>
    ///// Remueve al usuario del grupo de chat correspondiente a chatId.
    ///// </summary>
    //[Authorize]
    //public async Task RemoveFromGroup(string chatId)
    //{
    //    if (string.IsNullOrWhiteSpace(chatId))
    //    {
    //        Console.WriteLine("[ChatHub] chatId inválido en RemoveFromGroup.");
    //        return;
    //    }

    //    await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId);
    //}

    /// <summary>
    /// Remueve al cliente del grupo de un chat (basado en MSTeamsChatId).
    /// </summary>
    [Authorize]
    public async Task LeaveChatGroup(string msTeamsChatId)
    {
        if (string.IsNullOrWhiteSpace(msTeamsChatId))
        {
            Console.WriteLine("[ChatHub] msTeamsChatId inválido en LeaveChatGroup.");
            return;
        }

        var groupName = $"{ChatGroupPrefix}{msTeamsChatId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
