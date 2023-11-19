using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BulletSimulationInterface : MonoBehaviour
{
    // Armour Penetration Simulation Camera
    [SerializeField] Camera simulationCamera;

    // Bullet Perspective Background
    Image perspBG;

    // Bullet Perspective
    Image perspective;

    Image bulletPenText;

    TextMeshProUGUI penText;

    // RUNTIME VARIABLES
    Texture2D renderTex2D;
    Sprite renderSprite;
    Rect texRect;

    void Start()
    {
        // Find all UI components and the render texture camera.
        perspBG = transform.Find("BulletPerspectiveBG").GetComponent<Image>();
        perspective = transform.Find("BulletPerspective").GetComponent<Image>();
        if (!perspBG) { throw new System.Exception("Missing UI Element - PerspBG {UNITYENGINE.UI CLASS}"); }

        bulletPenText = perspBG.transform.Find("BulletPenText").GetComponent<Image>();
        penText = bulletPenText.transform.Find("PenStatus").GetComponent<TextMeshProUGUI>();

        simulationCamera = GameObject.Find("BulletCamera").GetComponent<Camera>();
        if (simulationCamera == null) { throw new System.Exception("Bullet Simulation Camera Missing!"); }

        perspective.color = new Color32(255, 255, 255, 255); // Pesrpective is 255,255,255,0 before start to make UI Editing easier.

        // Perspective Elements are disabled when not simulating a bullet.
        perspBG.enabled = false;
        perspective.enabled = false;
        bulletPenText.enabled = false;
        penText.enabled = false;

        BindRenderTextureSprite();

        // Enable and disable Perspective + Perspective Background interface elements.
        SimulationEngine.OnPlaybackReady += PlaybackStarted;
        SimulationEngine.OnPlaybackCancelled += PlaybackTerminated;
        SimulationEngine.OnPlaybackComplete += PlaybackEnded;
    }

    void Update()
    {
        RunSpriteRedefinition();
        //perspBG.enabled = Input.GetMouseButton(0);
        //perspective.enabled = perspBG.enabled;

        if (SimulationEngine.externalSimulationAnimationCompleted)
        {
            penText.text = SimulationEngine.simulatedPenetrationSuccessful ? "PENETRATION" : "NON-PENETRATION";
            penText.color = SimulationEngine.simulatedPenetrationSuccessful ? new Color32(255, 245, 93, 255) : new Color32(188, 188, 188, 255);
            bulletPenText.enabled = true;
            penText.enabled = true;
        }
        else
        {
            bulletPenText.enabled = false;
            penText.enabled = false;
        }
    }

    /// <summary>
    /// Binds the Render Texture to a Texture 2D that can be made into a sprite. <br/>
    /// Throws exception when the Simulation Camera has no target RenderTexture.
    /// </summary>
    /// <exception cref="System.Exception"></exception>
    void BindRenderTextureSprite()
    {
        if (simulationCamera.targetTexture == null) { throw new System.Exception("Render Texture Missing!"); }

        // Create a Texture2D and define the rect to copy.
        texRect = new Rect(0, 0, perspective.rectTransform.rect.width, perspective.rectTransform.rect.height);
        renderTex2D = new Texture2D((int)texRect.width, (int)texRect.height, TextureFormat.ARGB32, false);

        // Copy the texture over to the new Texture2D then create a Sprite using the Texture2D and earlier rect.
        // Then set the perspective hit viewer to the render sprite.
        Graphics.CopyTexture(simulationCamera.targetTexture, renderTex2D);
        renderSprite = Sprite.Create(renderTex2D, texRect, Vector2.zero);
        perspective.sprite = renderSprite;
    }

    /// <summary>
    /// Redefine the Sprite to sync with the render camera.
    /// </summary>
    void RunSpriteRedefinition()
    {
        Graphics.CopyTexture(simulationCamera.targetTexture, renderTex2D);
        renderSprite = Sprite.Create(renderTex2D, texRect, Vector2.zero);
        perspective.sprite = renderSprite;
    }

    void PlaybackStarted(GameObject bullet, Vector3 start, Vector3 end)
    {
        perspBG.enabled = true;
        perspective.enabled = true;
    }

    void PlaybackEnded(GameObject bullet, Vector3 start, Vector3 end)
    {
        perspBG.enabled = false;
        perspective.enabled = false;
    }

    void PlaybackTerminated()
    {
        PlaybackEnded(null, Vector3.zero, Vector3.zero);
    }
}
    