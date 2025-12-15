using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using System.Threading.Tasks;
using static Microsoft.Maui.ApplicationModel.Permissions;

namespace Global
{
    public sealed class PermissionHelper
    {
       // [ResolveFromContainer(typeof(PermissionHelper))]
        public static PermissionHelper Instance { get; set; }

        public async Task<bool> CheckAndRequest<T>(string info) where T : BasePermission, new()
        {
            var permission = new T();

            var status = await permission.CheckStatusAsync();

            if (status == PermissionStatus.Granted)
                return true;

            if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
                return false;

            status = await permission.RequestAsync();

            return status == PermissionStatus.Granted;

        }
    }
}

