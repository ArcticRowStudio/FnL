using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Platformer.Mechanics;

namespace Platformer.EditorTools
{
    public static class FreightElevatorPrefabBuilder
    {
        private const string SpritesPath = "Assets/Environment/Sprites/FreightElevator";
        private const string PrefabsPath = "Assets/Prefabs";
        private const string ElevatorPrefabFile = "Assets/Prefabs/FreightElevator.prefab";
        private const string CallStationPrefabFile = "Assets/Prefabs/ElevatorCallStation.prefab";

        [MenuItem("Tools/Freight Elevator/Build All Prefabs")]
        public static void BuildAllPrefabs()
        {
            if (!Directory.Exists(PrefabsPath))
            {
                Directory.CreateDirectory(PrefabsPath);
            }

            BuildElevatorPrefab();
            BuildCallStationPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[FreightElevator]</color> All prefabs built and saved to " + PrefabsPath);
        }

        private static Sprite LoadSprite(string fileName)
        {
            string path = Path.Combine(SpritesPath, fileName).Replace("\\", "/");
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null)
            {
                Debug.LogWarning("[FreightElevator] Could not load sprite at: " + path);
            }
            return sp;
        }

        private static AudioClip LoadAudio(string audioName)
        {
            string path = "Assets/Audio/" + audioName;
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        public static GameObject BuildElevatorPrefab()
        {
            // 1. Root GameObject
            GameObject rootGo = new GameObject("FreightElevator");
            var controller = rootGo.AddComponent<FreightElevatorController>();
            var audioSource = rootGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.5f;

            // Load Audio Clips
            controller.pneumaticArrivalHissSound = LoadAudio("pneumatic_hiss.wav");
            controller.safetyBarLowerSound = LoadAudio("cartridge_insert.wav");
            controller.safetyBarRaiseSound = LoadAudio("button_click.wav");
            controller.arrivalThumpSound = LoadAudio("LandOnGround.wav");

            // 2. Guide Rails (Background static tracks)
            Sprite guideRailSprite = LoadSprite("FreightElevator_GuideRail.png");
            if (guideRailSprite != null)
            {
                GameObject railL = new GameObject("GuideRail_Left");
                railL.transform.SetParent(rootGo.transform, false);
                railL.transform.localPosition = new Vector3(-2.15f, 0f, 0f);
                var srL = railL.AddComponent<SpriteRenderer>();
                srL.sprite = guideRailSprite;
                srL.drawMode = SpriteDrawMode.Tiled;
                srL.size = new Vector2(0.32f, controller.travelHeight + 4f);
                srL.sortingOrder = 1;

                GameObject railR = new GameObject("GuideRail_Right");
                railR.transform.SetParent(rootGo.transform, false);
                railR.transform.localPosition = new Vector3(2.15f, 0f, 0f);
                var srR = railR.AddComponent<SpriteRenderer>();
                srR.sprite = guideRailSprite;
                srR.drawMode = SpriteDrawMode.Tiled;
                srR.size = new Vector2(0.32f, controller.travelHeight + 4f);
                srR.sortingOrder = 1;
            }

            // 3. Counterweight
            Sprite cwSprite = LoadSprite("FreightElevator_Counterweight.png");
            if (cwSprite != null)
            {
                GameObject cwGo = new GameObject("Counterweight");
                cwGo.transform.SetParent(rootGo.transform, false);
                cwGo.transform.localPosition = new Vector3(2.45f, controller.travelHeight, 0f);
                var cwSr = cwGo.AddComponent<SpriteRenderer>();
                cwSr.sprite = cwSprite;
                cwSr.sortingOrder = 2;
                controller.counterweightTransform = cwGo.transform;
            }

            // 4. Cabin Root (with Kinematic Rigidbody2D)
            GameObject cabinGo = new GameObject("Cabin");
            cabinGo.transform.SetParent(rootGo.transform, false);
            cabinGo.transform.localPosition = Vector3.zero;
            controller.cabinTransform = cabinGo.transform;

            var cabinRb = cabinGo.AddComponent<Rigidbody2D>();
            cabinRb.bodyType = RigidbodyType2D.Kinematic;
            cabinRb.simulated = true;

            // 4a. Back Mesh Grating (sortingOrder: 2)
            Sprite backMeshSprite = LoadSprite("FreightElevator_BackMesh.png");
            if (backMeshSprite != null)
            {
                GameObject meshGo = new GameObject("BackMesh");
                meshGo.transform.SetParent(cabinGo.transform, false);
                meshGo.transform.localPosition = new Vector3(0f, 0f, 0f);
                var sr = meshGo.AddComponent<SpriteRenderer>();
                sr.sprite = backMeshSprite;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(controller.elevatorWidth, 3.2f);
                sr.sortingOrder = 2;
            }

            // 4b. Platform Deck (Floor)
            Sprite platformSprite = LoadSprite("FreightElevator_Platform.png");
            GameObject platformGo = new GameObject("Platform_Deck");
            platformGo.transform.SetParent(cabinGo.transform, false);
            platformGo.transform.localPosition = Vector3.zero;

            var platformSr = platformGo.AddComponent<SpriteRenderer>();
            platformSr.sprite = platformSprite;
            platformSr.drawMode = SpriteDrawMode.Sliced;
            platformSr.size = new Vector2(controller.elevatorWidth, 0.6f);
            platformSr.sortingOrder = 3;

            // Platform solid floor collider (Width: 4.2, Height: 0.25, top surface at y=0)
            var floorCollider = platformGo.AddComponent<BoxCollider2D>();
            floorCollider.size = new Vector2(4.2f, 0.25f);
            floorCollider.offset = new Vector2(0f, -0.125f);
            controller.platformDeckCollider = floorCollider;

            // 4c. Side Railings & Colliders
            Sprite sideRailingSprite = LoadSprite("FreightElevator_SideRailing.png");
            if (sideRailingSprite != null)
            {
                // Left Railing
                GameObject leftRail = new GameObject("SideRailing_Left");
                leftRail.transform.SetParent(cabinGo.transform, false);
                leftRail.transform.localPosition = new Vector3(-2.1f, 0f, 0f);
                var leftSr = leftRail.AddComponent<SpriteRenderer>();
                leftSr.sprite = sideRailingSprite;
                leftSr.sortingOrder = 4;
                var leftCol = leftRail.AddComponent<BoxCollider2D>();
                leftCol.size = new Vector2(0.35f, 3.2f);
                leftCol.offset = new Vector2(0.1f, 1.6f);

                // Right Railing (Flipped)
                GameObject rightRail = new GameObject("SideRailing_Right");
                rightRail.transform.SetParent(cabinGo.transform, false);
                rightRail.transform.localPosition = new Vector3(2.1f, 0f, 0f);
                var rightSr = rightRail.AddComponent<SpriteRenderer>();
                rightSr.sprite = sideRailingSprite;
                rightSr.flipX = true;
                rightSr.sortingOrder = 4;
                var rightCol = rightRail.AddComponent<BoxCollider2D>();
                rightCol.size = new Vector2(0.35f, 3.2f);
                rightCol.offset = new Vector2(-0.1f, 1.6f);
            }

            // 4d. Top Lifting Truss Frame (sortingOrder: 7)
            Sprite topFrameSprite = LoadSprite("FreightElevator_TopFrame.png");
            if (topFrameSprite != null)
            {
                GameObject topGo = new GameObject("TopFrame");
                topGo.transform.SetParent(cabinGo.transform, false);
                topGo.transform.localPosition = new Vector3(0f, 3.2f, 0f);
                var sr = topGo.AddComponent<SpriteRenderer>();
                sr.sprite = topFrameSprite;
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(controller.elevatorWidth + 0.2f, 0.7f);
                sr.sortingOrder = 7;
            }

            // 4e. Safety Barrier Bar (sortingOrder: 7)
            Sprite barSprite = LoadSprite("FreightElevator_SafetyBar.png");
            GameObject barGo = new GameObject("SafetyBar");
            barGo.transform.SetParent(cabinGo.transform, false);
            barGo.transform.localPosition = new Vector3(0f, controller.safetyBarOpenY, 0f);
            controller.safetyBarTransform = barGo.transform;

            if (barSprite != null)
            {
                var barSr = barGo.AddComponent<SpriteRenderer>();
                barSr.sprite = barSprite;
                barSr.drawMode = SpriteDrawMode.Sliced;
                barSr.size = new Vector2(controller.elevatorWidth - 0.4f, 0.36f);
                barSr.sortingOrder = 7;
            }

            // 4f. Control Panel & Status Light (Inside cage on right post) with ElevatorButton!
            Sprite panelSprite = LoadSprite("FreightElevator_ControlPanel.png");
            if (panelSprite != null)
            {
                GameObject panelGo = new GameObject("ControlPanel");
                panelGo.transform.SetParent(cabinGo.transform, false);
                panelGo.transform.localPosition = new Vector3(1.85f, 1.25f, 0f);
                var sr = panelGo.AddComponent<SpriteRenderer>();
                sr.sprite = panelSprite;
                sr.sortingOrder = 7;

                var panelCol = panelGo.AddComponent<BoxCollider2D>();
                panelCol.isTrigger = true;
                panelCol.size = new Vector2(0.7f, 0.9f);

                // Status indicator light
                GameObject statusLightGo = new GameObject("StatusLED_Light2D");
                statusLightGo.transform.SetParent(panelGo.transform, false);
                statusLightGo.transform.localPosition = new Vector3(0f, 0.15f, -0.2f);
                var light2d = statusLightGo.AddComponent<Light2D>();
                light2d.lightType = Light2D.LightType.Point;
                light2d.pointLightInnerRadius = 0.05f;
                light2d.pointLightOuterRadius = 0.8f;
                light2d.color = controller.readyStatusColor;
                light2d.intensity = 1.0f;
                controller.statusPanelLight = light2d;

                // Interactive ElevatorButton on Control Panel
                var buttonComp = panelGo.AddComponent<ElevatorButton>();
                buttonComp.elevator = controller;
                buttonComp.action = ElevatorButton.ButtonAction.ToggleFloor;
                buttonComp.clickSound = LoadAudio("button_click.wav");
                buttonComp.buttonRenderer = sr;
                buttonComp.buttonLight = light2d;
            }

            // 4g. Amber Warning Beacon (Mounted on center of TopFrame)
            Sprite beaconSprite = LoadSprite("FreightElevator_Beacon.png");
            GameObject beaconGo = new GameObject("WarningBeacon");
            beaconGo.transform.SetParent(cabinGo.transform, false);
            beaconGo.transform.localPosition = new Vector3(0f, 3.4f, 0f);

            if (beaconSprite != null)
            {
                var beaconSr = beaconGo.AddComponent<SpriteRenderer>();
                beaconSr.sprite = beaconSprite;
                beaconSr.sortingOrder = 8;
                controller.beaconRenderer = beaconSr;
            }

            // Beacon Light2D
            GameObject beaconLightGo = new GameObject("Beacon_Light2D");
            beaconLightGo.transform.SetParent(beaconGo.transform, false);
            beaconLightGo.transform.localPosition = new Vector3(0f, 0.15f, -0.2f);
            var bLight = beaconLightGo.AddComponent<Light2D>();
            bLight.lightType = Light2D.LightType.Point;
            bLight.pointLightInnerRadius = 0.1f;
            bLight.pointLightOuterRadius = 2.8f;
            bLight.color = new Color(1.0f, 0.65f, 0.1f, 1.0f); // Amber
            bLight.intensity = 0.3f;
            controller.beaconLight = bLight;

            // 4h. Cabin Interior Work Lamp
            GameObject workLightGo = new GameObject("Cabin_WorkLight");
            workLightGo.transform.SetParent(cabinGo.transform, false);
            workLightGo.transform.localPosition = new Vector3(0f, 2.9f, -0.2f);
            var workLight = workLightGo.AddComponent<Light2D>();
            workLight.lightType = Light2D.LightType.Point;
            workLight.pointLightInnerRadius = 0.5f;
            workLight.pointLightOuterRadius = 3.2f;
            workLight.color = new Color(0.85f, 0.92f, 1.0f, 1.0f); // Cool white industrial fluorescent
            workLight.intensity = 0.75f;

            // 4i. Suspension Cables (Tiled upwards from TopFrame)
            Sprite cableSprite = LoadSprite("FreightElevator_Cable.png");
            if (cableSprite != null)
            {
                controller.cableRenderers = new SpriteRenderer[2];

                GameObject cableL = new GameObject("SuspensionCable_Left");
                cableL.transform.SetParent(cabinGo.transform, false);
                cableL.transform.localPosition = new Vector3(-1.1f, 3.4f, 0f);
                var srL = cableL.AddComponent<SpriteRenderer>();
                srL.sprite = cableSprite;
                srL.drawMode = SpriteDrawMode.Tiled;
                srL.size = new Vector2(0.16f, 12f);
                srL.sortingOrder = 2;
                controller.cableRenderers[0] = srL;

                GameObject cableR = new GameObject("SuspensionCable_Right");
                cableR.transform.SetParent(cabinGo.transform, false);
                cableR.transform.localPosition = new Vector3(1.1f, 3.4f, 0f);
                var srR = cableR.AddComponent<SpriteRenderer>();
                srR.sprite = cableSprite;
                srR.drawMode = SpriteDrawMode.Tiled;
                srR.size = new Vector2(0.16f, 12f);
                srR.sortingOrder = 2;
                controller.cableRenderers[1] = srR;
            }

            // 4j. Passenger Detection Trigger Zone (with ElevatorCabinTrigger forwarder)
            GameObject triggerGo = new GameObject("PassengerCabinTrigger");
            triggerGo.transform.SetParent(cabinGo.transform, false);
            triggerGo.transform.localPosition = Vector3.zero;
            var triggerCol = triggerGo.AddComponent<BoxCollider2D>();
            triggerCol.isTrigger = true;
            triggerCol.size = new Vector2(3.6f, 2.6f);
            triggerCol.offset = new Vector2(0f, 1.35f);
            controller.cabinPassengerTrigger = triggerCol;

            var forwarder = triggerGo.AddComponent<ElevatorCabinTrigger>();
            forwarder.controller = controller;

            // Apply full dimensions setup
            controller.ApplyDimensions();

            // Save Prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, ElevatorPrefabFile);
            Object.DestroyImmediate(rootGo);
            Debug.Log("<color=green>[FreightElevator]</color> Saved elevator prefab to: " + ElevatorPrefabFile);
            return prefab;
        }

        public static GameObject BuildCallStationPrefab()
        {
            GameObject stationGo = new GameObject("ElevatorCallStation");
            var triggerCol = stationGo.AddComponent<BoxCollider2D>();
            triggerCol.isTrigger = true;
            triggerCol.size = new Vector2(1.2f, 1.8f);
            triggerCol.offset = new Vector2(0f, 0.9f);

            var callStation = stationGo.AddComponent<ElevatorCallStation>();

            var audioSource = stationGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            callStation.callPressSound = LoadAudio("button_click.wav");

            Sprite stationSprite = LoadSprite("FreightElevator_CallStation.png");
            if (stationSprite != null)
            {
                var sr = stationGo.AddComponent<SpriteRenderer>();
                sr.sprite = stationSprite;
                sr.sortingOrder = 4;
                callStation.buttonRenderer = sr;
            }

            // Call indicator Light2D
            GameObject lightGo = new GameObject("CallLight_Light2D");
            lightGo.transform.SetParent(stationGo.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.45f, -0.2f);
            var light2d = lightGo.AddComponent<Light2D>();
            light2d.lightType = Light2D.LightType.Point;
            light2d.pointLightInnerRadius = 0.05f;
            light2d.pointLightOuterRadius = 1.0f;
            light2d.color = callStation.standbyColor;
            light2d.intensity = 0.8f;
            callStation.callButtonLight = light2d;

            // Also attach ElevatorButton for universal clicking / input!
            var buttonComp = stationGo.AddComponent<ElevatorButton>();
            buttonComp.elevator = null; // Will auto-find
            buttonComp.action = ElevatorButton.ButtonAction.CallToCurrentFloor;
            buttonComp.designatedFloor = callStation.designatedFloor;
            buttonComp.clickSound = LoadAudio("button_click.wav");
            buttonComp.buttonRenderer = callStation.buttonRenderer;
            buttonComp.buttonLight = light2d;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(stationGo, CallStationPrefabFile);
            Object.DestroyImmediate(stationGo);
            Debug.Log("<color=green>[FreightElevator]</color> Saved call station prefab to: " + CallStationPrefabFile);
            return prefab;
        }
    }
}
