using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using static SimulationEngine;

public class CameraPivoting : MonoBehaviour
{
    /// <summary>
    /// Main Camera reference
    /// </summary>
    Camera cam;

    /// <summary>
    /// Mouse Vector inputs
    /// </summary>
    Vector2 mouseVec;

    /// <summary>
    /// Speed Variable (raw). <br/>  Read Only.
    /// </summary>
    float m_speed = 1f;

    /// <summary>
    /// Used for safe read-write of the variable with built-in speed cap.
    /// </summary>
    float speed
    {
        get
        {
            return m_speed;
        }
        set
        {
            m_speed =  value > 0.5 && value < 5 ? value : m_speed;
        }
    }

    /// <summary>
    /// The Line Renderer used for previewing bullet trajectory.
    /// </summary>
    LineRenderer bulletPreview;

    /// <summary>
    /// The Simulation Bullet being tracked during simulation display. 
    /// </summary>
    GameObject simulationBullet = null;

    /// <summary>
    /// Has the player created a bullet preview by left-clicking on the armour?
    /// </summary>
    bool hasBulletPreview = false;

    /// <summary>
    /// Whether or not to show controls in OnGUI.
    /// </summary>
    bool showControlsInstead = false;

    void Start()
    {
        cam = Camera.main;
        mouseVec = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        bulletPreview = GetComponent<LineRenderer>();
        bulletPreview.startWidth = 0.05f;
        bulletPreview.endWidth = 0.05f;

        SimulationEngine.Initialise();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H)) { showControlsInstead = !showControlsInstead; }

        if (Input.GetMouseButton(1))
        {
            mouseVec = new Vector2(Input.GetAxis("Mouse Y"), -Input.GetAxis("Mouse X")) * speed;
            transform.rotation = Quaternion.Euler(transform.eulerAngles + new Vector3(mouseVec.x, mouseVec.y, 0));
        }
        else
        {
            mouseVec = Vector2.zero;
        }

        // Cache Axis and distance vector prematurely.
        Vector3 CameraDirection = cam.transform.position - transform.position;
        float scrollwheel = Input.GetAxis("Mouse ScrollWheel");

        // Alternating Inputs. To alter move speed, hold left shift then scroll.
        //                     To alter distance from center, just scroll.
        if (Input.GetKey(KeyCode.LeftShift))
        {
            speed += scrollwheel;
        }
        else
        {
            if (CameraDirection.magnitude > 5 && CameraDirection.magnitude < 30)
            {
                cam.transform.position += cam.transform.forward * scrollwheel * speed;
            }
            else
            {
                if (CameraDirection.magnitude < 5) { cam.transform.position -= (cam.transform.forward * 0.25f); }
                if (CameraDirection.magnitude > 30) { cam.transform.position += (cam.transform.forward * 0.25f); }
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (Input.GetKey (KeyCode.LeftShift))
            {
                bulletPreview.SetPositions(new Vector3[]
                {
                    cam.transform.position - cam.transform.forward,
                    cam.transform.position - cam.transform.forward
                });

                bulletPreview.enabled = false; // prevent this from being visible when the camera turns around and has the line renderer in sight.
                hasBulletPreview = false;
            }
            else
            {
                bulletPreview.enabled = true; // re-enable the line renderer.

                if (GetCurrentMousePositionAsRay(out RaycastHit info, out Vector3[] StartEndPoints))
                {
                    bulletPreview.SetPositions(StartEndPoints);
                }

                hasBulletPreview = true;
            }
        }
    }

    // Moved this to a separate method so we can use it for penetration pre-testing.
    /// <summary>
    /// Handle all mathematics to get the current mouse position on screen in 3D space as a ray to whatever it overlaps. <br/>
    /// Also returns the RaycastHit result if there was a hit.
    /// </summary>
    /// <param name="info"></param>
    /// <param name="StartEndPoints"></param>
    /// <returns></returns>
    private bool GetCurrentMousePositionAsRay(out RaycastHit info, out Vector3[] StartEndPoints)
    {
        info = default; StartEndPoints = new Vector3[2]; // Pre-set values to prevent issue.

        // ADD TO APPENDICIES: Directly converting the cursor position to a world point didn't work
        // Instead, getting the mouse direction from the center of the screen and adding it to half
        // the Screen Width and Height gave the correct vector direction.
        Ray hitRay = cam.ScreenPointToRay(Input.mousePosition);
        bool result = Physics.Raycast(hitRay, out RaycastHit i, 9999f);

        if (result)
        {
            info = i;

            Vector3 camTransformSP = cam.WorldToScreenPoint(cam.transform.position + cam.transform.forward);
            Vector3 mouseDir = Input.mousePosition - camTransformSP;
            Vector3 startVec3 = cam.ScreenToWorldPoint(new Vector3((Screen.width / 2 + mouseDir.x), (Screen.height / 2) + mouseDir.y));
            Vector3 dir = (i.point - startVec3).normalized;

            StartEndPoints = new Vector3[]
            {
                    startVec3 + (dir*5),
                    i.point
            };
        }

        return result;
    }

    #region IMGUI
    private void OnGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.Label("Press \"H\" to " + (showControlsInstead ? "Hide Controls." : "Show Controls."));
        GUILayout.Label(string.Format("Camera Speed Multiplier: {0:F}x", m_speed));

        if (showControlsInstead)
        {
            ControlsInfo();
        }
        else
        {
            SimulationData();
        }
        GUILayout.EndVertical();

        PenetrationPossibilityFrame();

        if (hasBulletPreview)
        {
            if (GUI.Button(new Rect(5f, Screen.height - 25f, 120f, 20f), "SIMULATE"))
            {
                // If a prior simulation was interrupted, remove it's bullet.
                if (simulationBullet != null)
                {
                    Destroy(simulationBullet);
                }

                // Hook to Delegates for listening.
                SimulationEngine.OnPlaybackReady += StartBulletPlayback;
                SimulationEngine.OnPlaybackCancelled += StopBulletPlayback;

                // Start simulation.
                SimulationEngine.StartSimulation(bulletPreview);
            }
        }
    }
    
    /// <summary>
    /// Control Scheme Info.
    /// </summary>
    private void ControlsInfo()
    {
        GUILayout.Label("Left Mouse - Create Bullet Trajectory");
        GUILayout.Label("Shift + Left Mouse - Clear Bullet Trajectory");
        GUILayout.Space(15f);

        GUILayout.Label("Scroll Wheel - Zoom in or Out");
        GUILayout.Label("Shift + Scroll Wheel - Change Camera Movement Speed");
    }

    /// <summary>
    /// The Input Fields for Simulation data.
    /// </summary>
    private void SimulationData()
    {
        GUILayout.Space(15f);
        GUILayout.Label(string.Format("Actual Armour Width (Float): {0}mm", SimulationEngine.string_actualThickness));
        ArmourThicknessField();

        GUILayout.Space(5f);
        GUILayout.Label(string.Format("Bullet Penetration(Float): {0}mm", SimulationEngine.string_bulletPenetration));
        BulletPenetrationField();
    }

    private void PenetrationPossibilityFrame()
    {
        // Predetermine penetration here.
        bool hoveringOverObject = GetCurrentMousePositionAsRay(out RaycastHit info, out Vector3[] StartEndPoints);
        if (!hoveringOverObject) { return; } // If there is nothing being hovered, don't bother drawing the possibility previewer.

        Vector2 MousePosAsGUIPoint = GUIUtility.ScreenToGUIPoint(new Vector2(Input.mousePosition.x, -Input.mousePosition.y)) + new Vector2(0, Screen.height);

        // Create a quick GUI Style for the penetration possibility table.
        GUIStyle style = new GUIStyle();
        style.normal.background = new Texture2D(1, 1);
        style.normal.background.SetPixel(0, 0, new Color32(46, 46, 46, 200));
        style.normal.background.Apply();

        // ADD TO APPENDICES: ScreenToGUIPoint was a trick I learnt while I started developing Unity Plugins & Cappuccino Editor Framework.
        // That, however, is not enough in this case as the interface is still off screen half the time along the y axis.
        // Firstly, inverting the y position of the mouse in the ScreenToGUIPoint gives us the correct y position, otherwise it'll end up going
        // offscreen in the oppsoite way. Adding Screen Height to the result of that (in MousePosAsGUIPoint) then 16f to both x and y here gives us an offset
        // that keeps the penetration possibility UI on the screen without it covering cursor input.
        GUILayout.BeginArea(new Rect(MousePosAsGUIPoint + new Vector2(16f, 16f),new Vector2(200, 66)), style );

            // Determine whether or not it's a penetration.
            bool predeterminePenValue = SimulationEngine.DeterminePenetration(StartEndPoints, out string EffectiveArmour, out string AngleOfAttack);

            // Create a style that mimics War Thunder's dynamic Red-Yellow-Green indicator.
            GUIStyle PenetrationPossibilityText = new GUIStyle();
            PenetrationPossibilityText.normal.textColor = predeterminePenValue ? Color.green : new Color32(232, 96, 100, 255);
            PenetrationPossibilityText.fontStyle = FontStyle.Bold;

            GUIStyle LightGreyText = new GUIStyle();
            LightGreyText.normal.textColor = Color.gray;

            GUILayout.Label(predeterminePenValue ? "Penetration Possible" : "Penetration not Possible.", PenetrationPossibilityText);
            GUILayout.Label($"Effective Armour: {EffectiveArmour}", GUILayout.Height(20f));
            GUILayout.Label($"Angle of Attack: {AngleOfAttack}", GUILayout.Height(20f)); // May need to look at inaccuracies.

        GUILayout.EndArea();
    }

    /// <summary>
    /// Create a float Text Field for Armour Thickness.
    /// </summary>
    private void ArmourThicknessField()
    {
        string sAT_cached = SimulationEngine.string_actualThickness;
        SimulationEngine.string_actualThickness = GUILayout.TextField(SimulationEngine.string_actualThickness, GUILayout.MaxWidth(100f));

        if (float.TryParse(SimulationEngine.string_actualThickness, 
            System.Globalization.NumberStyles.Float, 
            System.Globalization.CultureInfo.InvariantCulture.NumberFormat, 
            out float result))
        {
            SimulationEngine.actualThickness = result;
        }

        else
        {
            SimulationEngine.string_actualThickness = sAT_cached;
        }
    }

    /// <summary>
    /// Create a float Text Field for Bullet Penetration.
    /// </summary>
    private void BulletPenetrationField()
    {
        string sAT_cached = SimulationEngine.string_bulletPenetration;
        SimulationEngine.string_bulletPenetration = GUILayout.TextField(SimulationEngine.string_bulletPenetration, GUILayout.MaxWidth(100f));

        if (float.TryParse(SimulationEngine.string_bulletPenetration,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture.NumberFormat,
            out float result))
        {
            SimulationEngine.bulletPenetration = result;
        }

        else
        {
            SimulationEngine.string_bulletPenetration = sAT_cached;
        }
    }
    #endregion

    #region Bullet Playback
    void StopBulletPlayback()
    {
        // Stop all coroutines and unhook from delegates.
        StopAllCoroutines();
        SimulationEngine.OnPlaybackReady -= StartBulletPlayback;
        SimulationEngine.OnPlaybackCancelled -= StopBulletPlayback;
    }

    // Start bullet trajectory playback and cache the bullet being simulated.
    void StartBulletPlayback(GameObject bullet, Vector3 start, Vector3 end)
    {
        StartCoroutine(PlayBulletTravel(bullet, start, end));
        simulationBullet = bullet;
    }

    // Animate the bullet travelling to the target.
    IEnumerator PlayBulletTravel(GameObject bullet, Vector3 start, Vector3 end)
    {
        do
        {
            bullet.transform.position += (end - start).normalized * 15 * Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        while ((end - bullet.transform.position).magnitude > 0.1);

        SimulationEngine.OnPlaybackReady -= StartBulletPlayback;
        SimulationEngine.externalSimulationAnimationCompleted = true;

        yield return new WaitForSeconds(3); // Let the simulation rest so the user can inspect the final contact point.
        SimulationEngine.externalSimulationAnimationCompleted = false;
        SimulationEngine.OnPlaybackComplete(bullet, start, end);
    }
    #endregion
}
