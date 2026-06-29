using Microsoft.AspNetCore.SignalR;

namespace OpenStickyMemos.Api.Hubs;

public class NotesHub : Hub
{
    public async Task JoinProjectGroup(string projectId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, projectId);
    }

    public async Task LeaveProjectGroup(string projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, projectId);
    }

    public async Task NoteCreated(object note)
    {
        var projectId = Context.GetHttpContext()?.Request.Query["projectId"].ToString();
        if (!string.IsNullOrEmpty(projectId))
            await Clients.Group(projectId).SendAsync("NoteCreated", note);
    }

    public async Task NoteUpdated(object note)
    {
        var projectId = Context.GetHttpContext()?.Request.Query["projectId"].ToString();
        if (!string.IsNullOrEmpty(projectId))
            await Clients.Group(projectId).SendAsync("NoteUpdated", note);
    }

    public async Task NoteDeleted(string noteId, string projectId)
    {
        await Clients.Group(projectId).SendAsync("NoteDeleted", noteId);
    }
}
