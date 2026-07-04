using System;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using EduSmart.Models;
using System.Linq;

namespace EduSmart.Hubs
{
    public class ChatHub : Hub
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> ConnectedUsers = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();

        public void SendMessage(string userName, string userRole, string message)
        {
            if (string.IsNullOrWhiteSpace(userName))
                userName = "Bilinmeyen Kullanıcı";
            
            var avatar = userName.Substring(0, 1).ToUpper();
            var time = DateTime.Now.ToString("HH:mm");

            // Broadcast to everyone
            Clients.All.broadcastMessage(userName, userRole, avatar, message, time);
        }

        public void ConnectUser(string userName)
        {
            if (!string.IsNullOrWhiteSpace(userName))
            {
                ConnectedUsers.TryAdd(Context.ConnectionId, userName);
                Clients.All.userConnected(userName);
            }
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            if (ConnectedUsers.TryRemove(Context.ConnectionId, out string userName))
            {
                Clients.All.userDisconnected(userName);
            }
            return base.OnDisconnected(stopCalled);
        }
    }
}
