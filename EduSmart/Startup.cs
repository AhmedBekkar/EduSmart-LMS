using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(EduSmart.Startup))]

namespace EduSmart
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapSignalR();
        }
    }
}
