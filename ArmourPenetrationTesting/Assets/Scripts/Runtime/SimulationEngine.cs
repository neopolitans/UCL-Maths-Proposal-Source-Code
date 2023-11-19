using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public static class SimulationEngine
{
    // - PUBLIC VARS

    /// <summary>
    /// Whether or not the simulated penetration was successful. <br/><br/>
    /// Not to be mistaken for <seealso cref="externalSimulationAnimationCompleted"/>.
    /// </summary>
    public static bool simulatedPenetrationSuccessful
    {
        get { return m_simulatedPenetrationSuccessful; }
    }

    /// <summary>
    /// Whether the external animation method(s) have finished emulating the trajectory. <br/><br/>
    /// Not to be mistaken for <seealso cref="simulatedPenetrationSuccessful"/>.
    /// </summary>
    public static bool externalSimulationAnimationCompleted = false;

    /// <summary>
    /// The Actual Thickness of the Armour Plate.
    /// </summary>
    public static float actualThickness
    {
        get { return m_actualThickness; }
        set {if (m_simulationRunning) { return; } // Prevent altering the simulation mid-simulation.

            m_actualThickness = value; 

            if (m_armourPlate == null) { return; }

            float cmWidth = m_actualThickness / 100f;

            /// Convert mm into cm for representing in the armour plate.
            m_armourPlate.transform.localScale = new Vector3(
                m_armourPlate.transform.localScale.x,
                m_armourPlate.transform.localScale.y,
                cmWidth);
        }
    }

    /// <summary>
    /// The penetration value of the bullet.
    /// </summary>
    public static float bulletPenetration
    {
        get { return m_bulletPenetration; }
        set { 
            if (m_simulationRunning) { return; } // Prevent altering the simulation mid-simulation.
            m_bulletPenetration = value;
        }
    }

    /// <summary>
    /// The effective thickness of the tested armour.
    /// Only safe to use when a simulation is complete.
    /// </summary>
    public static float effectiveThickness = 0;

    public static GameObject armourPlate
    {
        get
        {
            return m_armourPlate;
        }
        set { m_armourPlate = value;

            float cmWidth = m_actualThickness / 100f;
            /// Convert mm into cm for representing in the armour plate.
            m_armourPlate.transform.localScale = new Vector3(
                m_armourPlate.transform.localScale.x,
                m_armourPlate.transform.localScale.y,
                cmWidth);
        }
    }

    /// <summary>
    /// Actual Thickness of armour as a String.
    /// </summary>
    public static string string_actualThickness = actualThickness.ToString();

    /// <summary>
    /// Effective Thickness of armour as a String.
    /// </summary>
    public static string string_effectiveThickness = "";

    public static string string_bulletPenetration = bulletPenetration.ToString();

    /// <summary>
    /// Is there a simulation currently running? (Read-Only)
    /// </summary>
    public static bool SimulationRunning
    {
        get { return m_simulationRunning; }
    }

    // - PRIVATE VARS

    private static float m_actualThickness = 150f;

    private static float m_bulletPenetration = 200f;

    private static bool m_simulationRunning = false;

    private static bool m_simulatedPenetrationSuccessful = false;

    private static GameObject m_armourPlate = null;

    private static GameObject bullet;

    // - DELEGATES

    public delegate void SimulationEngineEvent();
    public static SimulationEngineEvent OnStartSimulation; // Called when a simulation starts
    public static SimulationEngineEvent OnStopSimulation; // Called when a simulation is interrupted
    public static SimulationEngineEvent OnEndSimulation; // Called when a simulation is ending.

    public static SimulationEngineEvent OnPlaybackCancelled; // Called when playback of a simulated bullet is cancelled for IEnumerators.

    public delegate void SimulationBulletEvent(GameObject bullet, Vector3 start, Vector3 end);
    public static SimulationBulletEvent OnPlaybackReady; // Called when bullet playback is ready.
    public static SimulationBulletEvent OnPlaybackComplete; // Called when bullet playback is completed by external scripts for cleanup.

    // - METHODS

    public static void Initialise()
    {
        armourPlate = GameObject.Find("ArmourPlate");
        actualThickness = 150f;
        string_actualThickness = actualThickness.ToString();
        string_bulletPenetration = bulletPenetration.ToString();

        OnPlaybackComplete = TidyUpPlayback;
    }

    /// <summary>
    /// Start an Armour Penetration simulation.
    /// </summary>
    /// <param name="previewedTrajectory">The line renderer used to preview trajectory.</param>
    public static void StartSimulation(LineRenderer previewedTrajectory)
    {
        if (m_simulationRunning)
        {
            // Stop the simulation and cancel any active playback.
            if (OnStopSimulation != null && OnStopSimulation.GetInvocationList().Length > 0) { OnStopSimulation(); }
            if (OnPlaybackCancelled != null && OnPlaybackCancelled.GetInvocationList().Length > 0) { OnPlaybackCancelled(); }

            // Destroy the simulation bullet.
            Object.Destroy(bullet);

            // Reset the simulationRunning status.
            m_simulationRunning = false;
        }

        // Notify listeners to simulation starting.
        if (OnStartSimulation != null && OnStartSimulation.GetInvocationList().Length > 0) { OnStartSimulation(); }
        m_simulationRunning = true;

        // Create the bullet 
        bullet = Object.Instantiate(Resources.Load("Prefabs/Bullet", typeof(GameObject))) as GameObject;

        // Set the simulation bullet's position and rotation relative to the trajectory's direction (Bullet Forward Diection) and the world up vector.
        bullet.transform.position = previewedTrajectory.GetPosition(0);
        bullet.transform.rotation = Quaternion.LookRotation(previewedTrajectory.GetPosition(1) - previewedTrajectory.GetPosition(0), Vector3.up);

        m_simulatedPenetrationSuccessful = DeterminePenetration(new Vector3[] { previewedTrajectory.GetPosition(0), previewedTrajectory.GetPosition(1) }, out string_effectiveThickness, out string AoA);

        // Trigger playback of the bullet simulation.
        if (OnPlaybackReady != null && OnPlaybackReady.GetInvocationList().Length > 0) { OnPlaybackReady(bullet, previewedTrajectory.GetPosition(0), previewedTrajectory.GetPosition(1)); }

        // End the simulation here.
        m_simulationRunning = false;
        if (OnEndSimulation != null && OnEndSimulation.GetInvocationList().Length > 0) { OnEndSimulation(); }
    }

    private static void TidyUpPlayback(GameObject bullet, Vector3 start, Vector3 end)
    {
        Object.Destroy(bullet);
    }

    /// <summary>
    /// Determine whether the current scenario will result in penetration. Be it through preview or during simulation. <br/>
    /// Moved from StartSimulation() for ease of viewing.
    /// </summary>
    /// <param name="StartEndPoints">The start and end points to test.</param>
    /// <param name="EffectiveThicknessString">The output string to provide the armour's current value.</param>
    /// <returns></returns>
    public static bool DeterminePenetration(Vector3[] StartEndPoints, out string EffectiveThicknessString, out string angleOfAttack)
    {
        EffectiveThicknessString = default;
        angleOfAttack = default;

        // ADD TO APPENDICES: Testing for one normal (transform.up) meant that any rotation horizontally was not counted.
        // Testing for both and comparing which is greater resulted in a better accuracy, however it can still fail under conditions of odd rotations.
        float vert_test = GetEffectiveThickness((StartEndPoints[1] - StartEndPoints[0]).normalized, armourPlate.transform.up);
        float hori_test = GetEffectiveThickness((StartEndPoints[1] - StartEndPoints[0]).normalized, armourPlate.transform.right);
        effectiveThickness = vert_test > hori_test ? vert_test : hori_test;
        EffectiveThicknessString = effectiveThickness.ToString("N2") + "mm";

        // Get Angle of Attack Information
        float aoa_vert_test = GetAngleOfAttack((StartEndPoints[1] - StartEndPoints[0]).normalized, armourPlate.transform.up);
        float aoa_hori_test = GetAngleOfAttack((StartEndPoints[1] - StartEndPoints[0]).normalized, armourPlate.transform.right);
        angleOfAttack = (aoa_vert_test > aoa_hori_test ? aoa_vert_test : aoa_hori_test).ToString("N2") + "°";

        return bulletPenetration > effectiveThickness;
    }

    /// <summary>
    /// A direct translation of the method found in pages 6 and 7 of the mathematics proposal.
    /// </summary>
    /// <param name="direction">The projectile direction vector.</param>
    /// <param name="normal">The normal vector of the armour.</param>
    /// <returns></returns>
    public static float GetEffectiveThickness(Vector3 direction, Vector3 normal)
    {
        float VectorDotProduct = DotProduct(direction, normal);
        float VectorMags = VectorMagnitude(direction) * VectorMagnitude(normal);

        float arccos = Mathf.Acos(VectorDotProduct / VectorMags);

        // ADD TO APPENDICIES: Unlike how it was expected, the sine function in Unity's Mathf library expects
        // a rotation in *radians*, not degrees. So we do away with the (180/PI) multiplication for the thickness.
        return actualThickness / Mathf.Sin(arccos);
    }

    public static float GetAngleOfAttack(Vector3 direction, Vector3 normal)
    {
        float VectorDotProduct = DotProduct(direction, normal);
        float VectorMags = VectorMagnitude(direction) * VectorMagnitude(normal);

        float arccos = Mathf.Acos(VectorDotProduct / VectorMags);

        return arccos * (180 / Mathf.PI);
    }

    // ADD TO APPENDICES: Built-in methods gave a slightly lager margin of error.
    // Adding these two rectified this and gave a better margin of error (within +-3%).

    /// <summary>
    /// Direct implementation of Dot Product found in pages 6 and 7 of the mathematics proposal.
    /// </summary>
    /// <param name="a">Vector A</param>
    /// <param name="b">Vector B</param>
    /// <returns></returns>
    public static float DotProduct(Vector3 a, Vector3 b) => (a.x * b.x) + (a.y * b.y) + (a.z * b.z);

    /// <summary>
    /// Direct implementation of Vector Magnitude found in pages 6 and 7 of the mathematics proposal.
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    public static float VectorMagnitude(Vector3 v) => (v.x * v.x) + (v.y * v.y) + (v.z * v.z);
}
