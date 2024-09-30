using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;


namespace Models
{
    public class CameraSwitcher
    {
        static List<CinemachineVirtualCamera> cameras = new List<CinemachineVirtualCamera>();

        private static CinemachineVirtualCamera activeCamera = null;

        public static bool isActiveCamera(CinemachineVirtualCamera cam)
        {
            return cam == activeCamera;
        }

        public static void switchCamera(CinemachineVirtualCamera cam)
        {
            cam.Priority = 10;
            activeCamera = cam;
            foreach (CinemachineVirtualCamera c in cameras)
            {
                if (c != cam && c.Priority != 0)
                {
                    c.Priority = 0;
                }
            }
        }

        public static void Register(CinemachineVirtualCamera cam)
        {
            cameras.Add(cam);
        }

        public static void Unregister(CinemachineVirtualCamera cam)
        {
            cameras.Remove(cam);
        }

    }
}
