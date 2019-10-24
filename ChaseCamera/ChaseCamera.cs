using MSCLoader;
using UnityEngine;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System.Collections.Generic;
using System.Globalization;

namespace ChaseCamera
{
    public class ChaseCamera : Mod
    {
        public override string ID => "ChaseCamera"; //Your mod ID (unique)
        public override string Name => "ChaseCamera"; //You mod name
        public override string Author => "cbethax"; //Your Username
        public override string Version => "1.1.0"; //Version
        public override bool UseAssetsFolder => false;

        readonly Keybind showGuiKeyBind = new Keybind("ChaseCameraKey", "Chase Camera Key", KeyCode.C);


        Settings settingsSmoothFollow;
        Settings settingsSmoothLook;
        Settings settingsResetAfterInactivity;
        Settings settingsResetAfterTime;

        GameObject player;
        GameObject fpsCameraParent;
        GameObject fpsCameraChild;
        GameObject crosshair;
        GameObject targetVehicle;
        GameObject chaseSphere;
        GameObject chaseCamera;

        Vector3 velocity;

        float lookAtVelocity;

        CameraOffset cameraOffset;

        bool isCameraActive = false;

        float cameraMoveInactive;

        public static float smoothFollow = 0.05f;

        public static float smoothLook = 0.05f;

        public static bool resetAfterInactivity = true;

        public static float resetAfterTime = 2f;

        public static List<CameraOffset> vehicleCameras = new List<CameraOffset>()
        {
            new CameraOffset("Satsuma", "SATSUMA(557kg, 248)", new Vector3(0f, 1.5f, 4f), new Vector3(0f, 1f, 0f)),
            new CameraOffset("Ruscko", "RCO_RUSCKO12(270)", new Vector3(0f, 2f, 4.5f), new Vector3(0f, 1f, 0f)),
            new CameraOffset("Kekmet", "KEKMET(350-400psi)", new Vector3(0f, 4f, 9f), new Vector3(0f, 2f, 0f)),
            new CameraOffset("Gifu", "GIFU(750/450psi)", new Vector3(0f, 5f, 8f), new Vector3(0f, 2f, 0f)),
            new CameraOffset("Hayosiko", "HAYOSIKO(1500kg, 250)", new Vector3(0f, 2.5f, 4f), new Vector3(0f, 1f, 0f)),
            new CameraOffset("Ferndale", "FERNDALE(1630kg)", new Vector3(0f, 2f, 5f), new Vector3(0f, 1f, 0f)),
            new CameraOffset("Second Ferndale (Mod)", "FERNDALE(1630kg)(Clone)", new Vector3(0f, 2f, 5f), new Vector3(0f, 1f, 0f))
        };

        public ChaseCamera()
        {
            settingsResetAfterInactivity = new Settings("resetAfterInactivity", "Reset camera on inactivity", resetAfterInactivity, () => ApplySettings());
            settingsSmoothFollow = new Settings("smoothFollow", "Follow Smooth", smoothFollow, () => ApplySettings());
            settingsSmoothLook = new Settings("smoothLook", "Look Smooth", smoothLook, () => ApplySettings());
            settingsResetAfterTime = new Settings("resetAfterTime", "Inactivity time", resetAfterTime, () => ApplySettings());
        }

        public override void OnLoad()
        {
            Keybind.Add(this, showGuiKeyBind);

            player = GameObject.Find("PLAYER");
            fpsCameraParent = GameObject.Find("PLAYER/Pivot/AnimPivot/Camera/FPSCamera");
            fpsCameraChild = GameObject.Find("PLAYER/Pivot/AnimPivot/Camera/FPSCamera/FPSCamera");
            crosshair = GameObject.Find("GUI/Icons/GUITexture");
            chaseSphere = new GameObject("ChaseSphere");
            chaseCamera = new GameObject("ChaseCamera");
        }

        public override void ModSettings()
        {
            Settings.AddText(this, "Configuration");
            Settings.AddSlider(this, settingsSmoothFollow, 0f, 0.25f);
            Settings.AddSlider(this, settingsSmoothLook, 0f, 0.25f);
            Settings.AddCheckBox(this, settingsResetAfterInactivity);
            Settings.AddSlider(this, settingsResetAfterTime, 1f, 10f);

            foreach (CameraOffset cameraOffset in vehicleCameras)
            {
                Settings.AddText(this, cameraOffset.name);
                Settings.AddSlider(this, cameraOffset.settingsOffsetY, 0f, 20f);
                Settings.AddSlider(this, cameraOffset.settingsOffsetZ, 0f, 20f);
                Settings.AddSlider(this, cameraOffset.settingsLookAtOffsetY, 0f, 10f);
            }
        }

        public override void ModSettingsLoaded()
        {
            ApplySettings();

            foreach (CameraOffset cameraOffset in vehicleCameras)
            {
                cameraOffset.ApplySettings();
            }
        }

        public override void Update()
        {
            bool isSeated = FsmVariables.GlobalVariables.FindFsmBool("PlayerSeated").Value || FsmVariables.GlobalVariables.FindFsmString("PlayerCurrentVehicle").Value != "";

            if (!isSeated && isCameraActive || showGuiKeyBind.IsDown())
            {
                ToggleCamera();
            }
        }

        public override void FixedUpdate()
        {
            if (isCameraActive)
            {
                if(!FsmVariables.GlobalVariables.FindFsmBool("PlayerInMenu").Value)
                {
                    float mouseMoveX = Input.GetAxis("Mouse X");
                    float mouseMoveY = Input.GetAxis("Mouse Y");
                    float mouseWheel = Input.mouseScrollDelta.y;

                    if (Mathf.Abs(mouseMoveX) < 0.02f && Mathf.Abs(mouseMoveY) < 0.02f)
                    {
                        cameraMoveInactive -= Time.deltaTime;
                    }
                    else
                    {
                        cameraMoveInactive = resetAfterTime;
                        chaseSphere.transform.localEulerAngles = new Vector3(chaseSphere.transform.localEulerAngles.x - 0.5f * Input.GetAxis("Mouse Y"), chaseSphere.transform.localEulerAngles.y + 0.5f * Input.GetAxis("Mouse X"), chaseSphere.transform.localEulerAngles.z);
                    }

                    if (Mathf.Abs(mouseWheel) > 0.01f)
                    {
                        cameraOffset.offset.z = Mathf.Clamp(cameraOffset.offset.z + mouseWheel * 0.25f, -20f, 0f);
                    }

                    if (cameraMoveInactive <= 0f && resetAfterInactivity)
                    {
                        chaseSphere.transform.localRotation = Quaternion.Slerp(chaseSphere.transform.localRotation, Quaternion.identity, 2.5f * Time.deltaTime);
                    }
                }

                Vector3 lookAtTarget = chaseSphere.transform.position + chaseSphere.transform.right * cameraOffset.lookAtOffset.x + chaseSphere.transform.up * cameraOffset.lookAtOffset.y + chaseSphere.transform.forward * cameraOffset.lookAtOffset.z;
                Vector3 chasePivot = chaseSphere.transform.position + chaseSphere.transform.up * cameraOffset.offset.y + chaseSphere.transform.forward * cameraOffset.offset.z;

                chaseCamera.transform.position = Vector3.SmoothDamp(chaseCamera.transform.position, chasePivot, ref velocity, smoothFollow);

                Quaternion targetRotation = Quaternion.LookRotation(lookAtTarget - chaseCamera.transform.position, Vector3.up);
                float deltaAngle = Quaternion.Angle(chaseCamera.transform.rotation, targetRotation);

                if (deltaAngle > 0.0f)
                {
                    float t = Mathf.SmoothDampAngle(deltaAngle, 0.0f, ref lookAtVelocity, smoothLook);
                    t = 1.0f - t / deltaAngle;

                    chaseCamera.transform.rotation = Quaternion.Slerp(chaseCamera.transform.rotation, targetRotation, t);
                }
            }
        }

        void ApplySettings()
        {
            smoothFollow = float.Parse(settingsSmoothFollow.GetValue().ToString());
            smoothLook = float.Parse(settingsSmoothLook.GetValue().ToString());
            resetAfterInactivity = (bool)settingsResetAfterInactivity.GetValue();
            resetAfterTime = float.Parse(settingsResetAfterTime.GetValue().ToString());
        }

        public static int IndexOfVehicleCamera(string gameObjectName)
        {
            int index = -1;

            for (int i = 0; i < vehicleCameras.Count; i++)
            {
                if (vehicleCameras[i].gameObjectName == gameObjectName)
                {
                    index = i;
                    break;
                }
            }

            return index;
        }

        public void ToggleCamera()
        {
            bool isSeated = FsmVariables.GlobalVariables.FindFsmBool("PlayerSeated").Value || FsmVariables.GlobalVariables.FindFsmString("PlayerCurrentVehicle").Value != "";
            Transform currentVehicle = player.transform.root;
            int vehicleCameraIdx = IndexOfVehicleCamera(currentVehicle.name);


            if (vehicleCameraIdx > -1)
            {
                cameraOffset = vehicleCameras[vehicleCameraIdx];

                targetVehicle = currentVehicle.gameObject;
            }
            else
            {
                targetVehicle = null;
            }

            isCameraActive = !isCameraActive && isSeated && targetVehicle != null;

            if (isCameraActive)
            {

                crosshair.SetActive(false);
                SetupCamera();
            }
            else
            {
                crosshair.SetActive(true);
                ResetCamera();
            }
        }

        void SetupCamera()
        {
            chaseSphere.transform.parent = targetVehicle.transform;
            chaseSphere.transform.localPosition = Vector3.zero;
            chaseSphere.transform.localRotation = Quaternion.identity;

            chaseCamera.transform.position = chaseSphere.transform.position;
            chaseCamera.transform.rotation = chaseSphere.transform.rotation;

            fpsCameraChild.transform.SetParent(chaseCamera.transform, false);
        }

        void ResetCamera()
        {
            fpsCameraChild.transform.SetParent(fpsCameraParent.transform, false);
        }
    }
}
