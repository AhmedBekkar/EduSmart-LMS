using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using EduSmart.Models;

namespace EduSmart.Services
{
    public class NotificationService
    {
        private static readonly string AppDataFolder = HttpContext.Current.Server.MapPath("~/App_Data");
        private static readonly string NotificationsFile = Path.Combine(AppDataFolder, "notifications.json");
        private static readonly object _lock = new object();

        private static List<Notification> GetNotifications()
        {
            lock (_lock)
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                if (!File.Exists(NotificationsFile))
                {
                    return new List<Notification>();
                }

                var json = File.ReadAllText(NotificationsFile);
                if (string.IsNullOrWhiteSpace(json))
                    return new List<Notification>();

                var js = new JavaScriptSerializer();
                return js.Deserialize<List<Notification>>(json) ?? new List<Notification>();
            }
        }

        private static void SaveNotifications(List<Notification> notifications)
        {
            lock (_lock)
            {
                // Keep only the latest 200 notifications to prevent file bloat
                var recentNotifications = notifications.OrderByDescending(n => n.CreatedAt).Take(200).ToList();
                var js = new JavaScriptSerializer();
                var json = js.Serialize(recentNotifications);
                File.WriteAllText(NotificationsFile, json);
            }
        }

        public static void AddNotification(Notification notification)
        {
            var list = GetNotifications();
            list.Insert(0, notification);
            SaveNotifications(list);
            
            // Push via SignalR to connected clients
            var hubContext = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<EduSmart.Hubs.ChatHub>();
            
            if (notification.UserId.HasValue)
            {
                // Push to specific user via hub client broadcast with check in JS
                hubContext.Clients.All.receiveNotification(notification);
            }
            else
            {
                // Push to all
                hubContext.Clients.All.receiveNotification(notification);
            }
        }

        public static List<Notification> GetUserNotifications(int userId, string role)
        {
            var list = GetNotifications();
            return list.Where(n => 
                (n.UserId == userId) || 
                (!n.UserId.HasValue && (n.Role == "All" || n.Role == role))
            )
            .OrderByDescending(n => n.CreatedAt)
            .ToList();
        }

        public static void MarkAsRead(string id)
        {
            var list = GetNotifications();
            var notif = list.FirstOrDefault(n => n.Id == id);
            if (notif != null)
            {
                notif.IsRead = true;
                SaveNotifications(list);
            }
        }
        
        public static void MarkAllAsRead(int userId, string role)
        {
            var list = GetNotifications();
            bool changed = false;
            foreach(var n in list)
            {
                if (!n.IsRead && (n.UserId == userId || (!n.UserId.HasValue && (n.Role == "All" || n.Role == role))))
                {
                    n.IsRead = true;
                    changed = true;
                }
            }
            if (changed) SaveNotifications(list);
        }
    }
}
