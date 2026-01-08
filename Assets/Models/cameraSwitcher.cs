using System.Collections.Generic;
using Cinemachine;

namespace Models
{
    public class CameraSwitcher
    {
        static List<CinemachineVirtualCamera> cameras = new List<CinemachineVirtualCamera>();
        private static CinemachineVirtualCamera activeCamera = null;

        public static bool isActiveCamera(CinemachineVirtualCamera cam) => cam == activeCamera;

        public static void switchCamera(CinemachineVirtualCamera cam)
        {
            if (cam == null) return;

            activeCamera = cam;
            cam.Priority = 10;

            foreach (var c in cameras)
            {
                if (c != null && c != cam) c.Priority = 0;
            }
        }

        public static void Register(CinemachineVirtualCamera cam)
        {
            if (cam == null) return;
            if (!cameras.Contains(cam)) cameras.Add(cam);
        }

        public static void Unregister(CinemachineVirtualCamera cam)
        {
            cameras.Remove(cam);
            if (activeCamera == cam) activeCamera = null;
        }
    }
}
