using System.Collections.Generic;
using System.IO;
using System.Threading;
using MSCLoader;
using UnityEngine;
using HutongGames.PlayMaker;
using Newtonsoft.Json;

namespace ChaseCamera
{
    public class ChaseCamera : Mod
    {
        public override string ID => "ChaseCamera"; //Your mod ID (unique)
        public override string Name => "ChaseCamera"; //You mod name
        public override string Author => "cbethax"; //Your Username
        public override string Version => "1.4.1"; //Version
        public override bool UseAssetsFolder => true;

        readonly Keybind chaseCameraKeyBind = new Keybind("ChaseCameraKey", "Chase Camera Key", KeyCode.C);
        readonly Keybind toggleLookKeyBind = new Keybind("ToggleLook", "Toggle Look Key", KeyCode.LeftControl);
        readonly Keybind lookBehindKeyBind = new Keybind("LookBehind", "Look Behind Key", KeyCode.Tab);

        Settings settingsSmoothFollow;
        Settings settingsSmoothLook;
        Settings settingsToggleLook;
        Settings settingsResetAfterInactivity;
        Settings settingsResetAfterTime;
        Settings settingsShowSpeedAndRpm;
        Settings settingsLookBehindToggle;
        Settings settingsOffsetY;
        Settings settingsOffsetZ;
        Settings settingsLookAtOffsetY;
        Settings settingsAudioDistance;

        GameObject player;
        GameObject fpsCameraParent;
        GameObject fpsCameraChild;
        GameObject crosshair;
        GameObject targetVehicle;
        GameObject chaseSphere;
        GameObject chaseCamera;

        MonoBehaviour mouseLookX;
        MonoBehaviour mouseLookY;

        Vector3 velocity;

        float lookAtVelocity;

        CameraOffset cameraOffset;

        bool isCameraActive = false;

        float cameraMoveInactive;

        public static float smoothFollow;
        public static float smoothLook;
        public static bool toggleLook;
        public static bool resetAfterInactivity;
        public static float resetAfterTime;
        public static bool showSpeedAndRpm;
        public static bool lookBehindToggle;

        GameObject guiSpeed;
        TextMesh guiSpeedTextMesh;
        TextMesh guiSpeedShadowMesh;
        GameObject guiRpm;
        TextMesh guiRpmTextMesh;
        TextMesh guiRpmShadowMesh;

        string configFilename = "config.json";
        Config config;

        public static List<CameraOffset> vehicleCameras = new List<CameraOffset>();

        Timer saveTimer;

        List<string> disabledRoots = new List<string>() { "BOAT", "PLAYER", "NPC_CARS", "TRAFFIC" };

        bool isLookBehindActive = false;
        bool isLookBehindActivated = false;

        public ChaseCamera()
        {
            string path = Path.Combine(ModLoader.GetModAssetsFolder(this), configFilename);
            string json = File.ReadAllText(path);
            config = JsonConvert.DeserializeObject<Config>(json);

            smoothFollow = config.smoothFollow;
            smoothLook = config.smoothLook;
            toggleLook = config.toggleLook;
            resetAfterInactivity = config.resetAfterInactivity;
            resetAfterTime = config.resetAfterTime;
            showSpeedAndRpm = config.showSpeedAndRpm;
            lookBehindToggle = config.lookBehindToggle;

            foreach (ConfigVehicle vehicle in config.vehicles)
            {
                vehicleCameras.Add(new CameraOffset(vehicle.name, vehicle.gameObject, new Vector3(vehicle.offset[0], vehicle.offset[1], vehicle.offset[2]), new Vector3(vehicle.lookAtOffset[0], vehicle.lookAtOffset[1], vehicle.lookAtOffset[2]), vehicle.audioMinDistance));
            }

            settingsResetAfterInactivity = new Settings("autoCenter", "Auto-center camera", resetAfterInactivity, () => ApplySettings());
            settingsSmoothFollow = new Settings("followSmooth", "Follow Smooth", smoothFollow, () => ApplySettings());
            settingsSmoothLook = new Settings("lookSmooth", "Look Smooth", smoothLook, () => ApplySettings());
            settingsToggleLook = new Settings("toggleLook", "Toggle Look", toggleLook, () => ApplySettings());
            settingsResetAfterTime = new Settings("autoCenterDelay", "Auto-center delay (seconds)", resetAfterTime, () => ApplySettings());
            settingsShowSpeedAndRpm = new Settings("showSpeedAndRpm", "Show Speed and RPM", showSpeedAndRpm, () => ApplySettings());
            settingsLookBehindToggle = new Settings("lookBehindToggle", "Look Behind Toggle", lookBehindToggle, () => ApplySettings());
            settingsOffsetY = new Settings("offsetY", "Offset Height", 0f, () => ApplySettings());
            settingsOffsetZ = new Settings("offsetZ", "Offset Length", 0f, () => ApplySettings());
            settingsLookAtOffsetY = new Settings("lookatOffsetY", "Look at Height", 0f, () => ApplySettings());
            settingsAudioDistance = new Settings("audioDistance", "Audio Distance", 0f, () => ApplySettings());
        }

        public override void OnLoad()
        {
            Keybind.Add(this, chaseCameraKeyBind);
            Keybind.Add(this, toggleLookKeyBind);
            Keybind.Add(this, lookBehindKeyBind);

            player = GameObject.Find("PLAYER");
            fpsCameraParent = GameObject.Find("PLAYER/Pivot/AnimPivot/Camera/FPSCamera");
            fpsCameraChild = GameObject.Find("PLAYER/Pivot/AnimPivot/Camera/FPSCamera/FPSCamera");
            crosshair = GameObject.Find("GUI/Icons/GUITexture");
            chaseSphere = new GameObject("ChaseSphere");
            chaseCamera = new GameObject("ChaseCamera");

            mouseLookX = FindMouseLook(player);
            mouseLookY = FindMouseLook(fpsCameraParent);

            InitGUI();
        }

        MonoBehaviour FindMouseLook(GameObject gameobject)
        {
            var components = gameobject.GetComponents<MonoBehaviour>();
            foreach (var component in components)
            {
                if (component.GetType().Name == "MouseLook")
                {
                    return component;
                }
            }

            return null;
        }

        void InitGUI()
        {
            GameObject guiIndicatorsContainer = GameObject.Find("GUI/Indicators");  
            GameObject guiGear = GameObject.Find("GUI/Indicators/Gear");

            guiSpeed = GameObject.Instantiate(guiGear);
            guiSpeed.name = "Speed";
            guiSpeedTextMesh = guiSpeed.GetComponent<TextMesh>();
            guiSpeedShadowMesh = guiSpeed.transform.GetChild(0).GetComponent<TextMesh>();
            guiRpm = GameObject.Instantiate(guiGear);
            guiRpm.name = "RPM";
            guiRpmTextMesh = guiRpm.GetComponent<TextMesh>();
            guiRpmShadowMesh = guiRpm.transform.GetChild(0).GetComponent<TextMesh>();

            GameObject.Destroy(guiSpeed.GetComponent<PlayMakerFSM>());
            GameObject.Destroy(guiRpm.GetComponent<PlayMakerFSM>());

            guiSpeed.transform.SetParent(guiIndicatorsContainer.transform, false);
            guiRpm.transform.SetParent(guiIndicatorsContainer.transform, false);
            guiSpeed.transform.localPosition = new Vector3(guiSpeed.transform.localPosition.x * -1, guiSpeed.transform.localPosition.y, guiSpeed.transform.localPosition.z);
            guiRpm.transform.localPosition = new Vector3(guiRpm.transform.localPosition.x * -1, guiRpm.transform.localPosition.y + guiRpm.transform.localPosition.y * 2, guiRpm.transform.localPosition.z);
            guiSpeedTextMesh.anchor = TextAnchor.UpperLeft;
            guiSpeedShadowMesh.anchor = TextAnchor.UpperLeft;
            guiRpmTextMesh.anchor = TextAnchor.UpperLeft;
            guiRpmShadowMesh.anchor = TextAnchor.UpperLeft;
            guiSpeed.SetActive(false);
            guiRpm.SetActive(false);
        }

        void ApplySettings()
        {
            smoothFollow = float.Parse(settingsSmoothFollow.GetValue().ToString());
            smoothLook = float.Parse(settingsSmoothLook.GetValue().ToString());
            toggleLook = (bool)settingsToggleLook.GetValue();
            resetAfterInactivity = (bool)settingsResetAfterInactivity.GetValue();
            resetAfterTime = float.Parse(settingsResetAfterTime.GetValue().ToString());
            showSpeedAndRpm = (bool)settingsShowSpeedAndRpm.GetValue();
            lookBehindToggle = (bool)settingsLookBehindToggle.GetValue();

            if (isCameraActive && guiSpeed && guiRpm)
            {
                guiSpeed.SetActive(showSpeedAndRpm);
                guiRpm.SetActive(showSpeedAndRpm);
            }

            if (cameraOffset != null) {
                cameraOffset.ApplySettings(float.Parse(settingsOffsetY.GetValue().ToString()), float.Parse(settingsOffsetZ.GetValue().ToString()), float.Parse(settingsLookAtOffsetY.GetValue().ToString()), float.Parse(settingsAudioDistance.GetValue().ToString()));

                UpdateAudio();
            }

            DelayedSave();
        }

        void DelayedSave()
        {
            if (saveTimer != null)
            {
                saveTimer.Dispose();
            }

            saveTimer = new Timer((obj) =>
            {
                SaveSettings();
            },
            null, 1000, Timeout.Infinite);
        }

        void SaveSettings()
        {
            string path = Path.Combine(ModLoader.GetModAssetsFolder(this), configFilename);

            config.smoothFollow = ChaseCamera.smoothFollow;
            config.smoothLook = ChaseCamera.smoothLook;
            config.toggleLook = ChaseCamera.toggleLook;
            config.resetAfterInactivity = ChaseCamera.resetAfterInactivity;
            config.resetAfterTime = ChaseCamera.resetAfterTime;
            config.showSpeedAndRpm = ChaseCamera.showSpeedAndRpm;
            config.lookBehindToggle = ChaseCamera.lookBehindToggle;
            config.vehicles = new List<ConfigVehicle>();

            for(int i = 0; i < vehicleCameras.Count; i++)
            {
                CameraOffset vehicleCamera = vehicleCameras[i];
                ConfigVehicle vehicle = new ConfigVehicle();

                vehicle.name = vehicleCamera.name;
                vehicle.gameObject = vehicleCamera.gameObjectName;
                vehicle.offset = new List<float>() { vehicleCamera.offset.x, vehicleCamera.offset.y, vehicleCamera.offset.z };
                vehicle.lookAtOffset = new List<float>() { vehicleCamera.lookAtOffset.x, vehicleCamera.lookAtOffset.y, vehicleCamera.lookAtOffset.z };
                vehicle.audioMinDistance = vehicleCamera.audioMinDistance;

                config.vehicles.Add(vehicle);
            }

            File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));

            saveTimer.Dispose();
            saveTimer = null;
        }

        public override void ModSettings()
        {
            Settings.AddHeader(this, "Base Settings");
            Settings.AddSlider(this, settingsSmoothFollow, 0f, 100f);
            Settings.AddSlider(this, settingsSmoothLook, 0f, 100f);
            Settings.AddCheckBox(this, settingsToggleLook);
            Settings.AddCheckBox(this, settingsResetAfterInactivity);
            Settings.AddSlider(this, settingsResetAfterTime, 1f, 10f);
            Settings.AddCheckBox(this, settingsShowSpeedAndRpm);
            Settings.AddCheckBox(this, settingsLookBehindToggle);

            Settings.AddHeader(this, "Current Vehicle Settings");
            Settings.AddSlider(this, settingsOffsetY, 0f, 20f);
            Settings.AddSlider(this, settingsOffsetZ, 0f, 20f);
            Settings.AddSlider(this, settingsLookAtOffsetY, 0f, 10f);
            Settings.AddSlider(this, settingsAudioDistance, 0f, 10f);
        }

        public override void ModSettingsLoaded()
        {
            settingsResetAfterInactivity.Value = resetAfterInactivity;
            settingsSmoothFollow.Value = smoothFollow;
            settingsSmoothLook.Value = smoothLook;
            settingsToggleLook.Value = toggleLook;
            settingsResetAfterTime.Value = resetAfterTime;
            settingsShowSpeedAndRpm.Value = showSpeedAndRpm;
            settingsLookBehindToggle.Value = lookBehindToggle;
            settingsOffsetY.Value = 0f;
            settingsOffsetZ.Value = 0f;
            settingsLookAtOffsetY.Value = 0f;
            settingsAudioDistance.Value = 0f;
        }

        public override void Update()
        {
            bool isSeated = FsmVariables.GlobalVariables.FindFsmString("PlayerCurrentVehicle").Value != "";

            if (!isSeated && isCameraActive || chaseCameraKeyBind.IsDown())
            {
                ToggleCamera();
            }

            if (isCameraActive)
            {
                mouseLookX.enabled = false;
                mouseLookY.enabled = false;
            }

            if (lookBehindToggle)
            {
                if (lookBehindKeyBind.IsDown())
                {
                    isLookBehindActive = !isLookBehindActive;
                }
            } else
            {
                isLookBehindActive = lookBehindKeyBind.IsPressed();
            }

            if (isLookBehindActive && !isLookBehindActivated)
            {
                isLookBehindActivated = true;
                chaseSphere.transform.localEulerAngles = new Vector3(chaseSphere.transform.localEulerAngles.x, chaseSphere.transform.localEulerAngles.y + 180f, chaseSphere.transform.localEulerAngles.z);
            } else if (!isLookBehindActive && isLookBehindActivated)
            {
                isLookBehindActivated = false;
                chaseSphere.transform.localEulerAngles = new Vector3(chaseSphere.transform.localEulerAngles.x, chaseSphere.transform.localEulerAngles.y - 180f, chaseSphere.transform.localEulerAngles.z);
            }
        }

        public override void FixedUpdate()
        {
            if (!isCameraActive)
            {
                return;
            }

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
                    if (!toggleLook || toggleLook && toggleLookKeyBind.IsPressed())
                    { 
                        cameraMoveInactive = resetAfterTime;

                        chaseSphere.transform.localEulerAngles = new Vector3(chaseSphere.transform.localEulerAngles.x - 0.5f * Input.GetAxis("Mouse Y"), chaseSphere.transform.localEulerAngles.y + 0.5f * Input.GetAxis("Mouse X"), chaseSphere.transform.localEulerAngles.z);
                    }
                }

                if (Mathf.Abs(mouseWheel) > 0.01f)
                {
                    cameraOffset.offset.z = Mathf.Clamp(cameraOffset.offset.z + mouseWheel * 0.25f, 0f, 20f);

                    DelayedSave();
                }

                if (cameraMoveInactive <= 0f && resetAfterInactivity && !isLookBehindActivated)
                {
                    chaseSphere.transform.localRotation = Quaternion.Slerp(chaseSphere.transform.localRotation, Quaternion.identity, 2.5f * Time.deltaTime);
                }
            }

            Vector3 lookAtTarget = chaseSphere.transform.position + chaseSphere.transform.right * cameraOffset.lookAtOffset.x + chaseSphere.transform.up * cameraOffset.lookAtOffset.y + chaseSphere.transform.forward * cameraOffset.lookAtOffset.z;
            Vector3 chasePivot = chaseSphere.transform.position + chaseSphere.transform.up * cameraOffset.offset.y + chaseSphere.transform.forward * cameraOffset.offset.z * -1f;

            chaseCamera.transform.position = Vector3.SmoothDamp(chaseCamera.transform.position, chasePivot, ref velocity, smoothFollow * (isLookBehindActivated ? 0f : 0.0025f));

            Quaternion targetRotation = Quaternion.LookRotation(lookAtTarget - chaseCamera.transform.position, Vector3.up);
            float deltaAngle = Quaternion.Angle(chaseCamera.transform.rotation, targetRotation);

            if (deltaAngle > 0.0f)
            {
                float t = Mathf.SmoothDampAngle(deltaAngle, 0.0f, ref lookAtVelocity, smoothLook * 0.0025f);
                t = 1.0f - t / deltaAngle;

                chaseCamera.transform.rotation = Quaternion.Slerp(chaseCamera.transform.rotation, targetRotation, t);
            }
        }

        public override void OnGUI()
        {
            if (isCameraActive && showSpeedAndRpm)
            {
                Drivetrain drivetrain = targetVehicle.GetComponent<Drivetrain>();
                Rigidbody rb = targetVehicle.GetComponent<Rigidbody>();

                if (drivetrain == null || rb == null)
                {
                    return;
                }

                string textSpeed = "Speed: " + Mathf.Round(rb.velocity.magnitude * 3.6f) + "km/h";
                string textRpm = "RPM: " + Mathf.Round(drivetrain.rpm);

                guiSpeedTextMesh.text = textSpeed;
                guiSpeedShadowMesh.text = textSpeed;
                guiRpmTextMesh.text = textRpm;
                guiRpmShadowMesh.text = textRpm;
            }
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
            bool isSeated = FsmVariables.GlobalVariables.FindFsmString("PlayerCurrentVehicle").Value != "";
            Transform currentVehicle = player.transform.root;
            int vehicleCameraIdx = IndexOfVehicleCamera(currentVehicle.name);
            
            isCameraActive = !isCameraActive && isSeated && !disabledRoots.Contains(currentVehicle.name);

            if (isCameraActive)
            {
                if (vehicleCameraIdx > -1)
                {
                    cameraOffset = vehicleCameras[vehicleCameraIdx];
                }
                else
                {
                    cameraOffset = new CameraOffset(currentVehicle.name, currentVehicle.name, new Vector3(0f, 1.5f, 4f), new Vector3(0f, 1f, 0f), 3f);

                    vehicleCameras.Add(cameraOffset);
                }

                targetVehicle = currentVehicle.gameObject;
                crosshair.SetActive(false);
                SetupCamera();

                if (showSpeedAndRpm)
                {
                    guiSpeed.SetActive(true);
                    guiRpm.SetActive(true);
                }

                settingsOffsetY.Value = cameraOffset.offset.y;
                settingsOffsetZ.Value = cameraOffset.offset.z;
                settingsLookAtOffsetY.Value = cameraOffset.lookAtOffset.y;
                settingsAudioDistance.Value = cameraOffset.audioMinDistance;
            }
            else
            {
                cameraOffset = null;
                targetVehicle = null;
                guiSpeed.SetActive(false);
                guiRpm.SetActive(false);
                crosshair.SetActive(true);
                ResetCamera();

                settingsOffsetY.Value = 0f;
                settingsOffsetZ.Value = 0f;
                settingsLookAtOffsetY.Value = 0f;
                settingsAudioDistance.Value = 0f;
            }
        }

        Dictionary<AudioSource, float> originalAudioSourceMinDistance = new Dictionary<AudioSource, float>();

        void SetupCamera()
        {
            chaseSphere.transform.parent = targetVehicle.transform;
            chaseSphere.transform.localPosition = Vector3.zero;
            chaseSphere.transform.localRotation = Quaternion.identity;

            chaseCamera.transform.position = chaseSphere.transform.position;
            chaseCamera.transform.rotation = chaseSphere.transform.rotation;

            fpsCameraChild.transform.SetParent(chaseCamera.transform, false);

            foreach (AudioSource audioSource in targetVehicle.GetComponentsInChildren<AudioSource>())
            {
                if (audioSource.clip == null)
                    continue;

                originalAudioSourceMinDistance.Add(audioSource, audioSource.minDistance);

                audioSource.minDistance *= cameraOffset.audioMinDistance;
            }
        }

        void ResetCamera()
        {
            fpsCameraChild.transform.SetParent(fpsCameraParent.transform, false);

            foreach (AudioSource audioSource in originalAudioSourceMinDistance.Keys)
            {
                if (audioSource.clip == null)
                    continue;

                audioSource.minDistance = originalAudioSourceMinDistance[audioSource];
            }

            originalAudioSourceMinDistance.Clear();
        }

        void UpdateAudio()
        {
            foreach (AudioSource audioSource in originalAudioSourceMinDistance.Keys)
            {
                if (audioSource.clip == null)
                    continue;

                audioSource.minDistance = originalAudioSourceMinDistance[audioSource] * cameraOffset.audioMinDistance;
            }
        }
    }
}
