using System.Web.Mvc;
using EduSmart.Services;

namespace EduSmart.Controllers
{
    public class NotificationsController : BaseController
    {
        [HttpGet]
        public JsonResult GetMyNotifications()
        {
            var userIdStr = Session["UserId"]?.ToString();
            var role = Session["UserRole"]?.ToString() ?? "Misafir";
            
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            }

            var notifications = NotificationService.GetUserNotifications(userId, role);
            
            return Json(new { success = true, data = notifications }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult MarkAsRead(string id)
        {
            NotificationService.MarkAsRead(id);
            return Json(new { success = true });
        }
        
        [HttpPost]
        public JsonResult MarkAllAsRead()
        {
            var userIdStr = Session["UserId"]?.ToString();
            var role = Session["UserRole"]?.ToString() ?? "Misafir";
            
            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int userId))
            {
                NotificationService.MarkAllAsRead(userId, role);
            }
            return Json(new { success = true });
        }
    }
}
