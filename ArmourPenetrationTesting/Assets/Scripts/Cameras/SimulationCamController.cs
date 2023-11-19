using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.ParticleSystem;

public class SimulationCamController : MonoBehaviour
{
    Camera self;

    bool isFollowingBullet = false;
    GameObject targetBullet = null;
    Vector3 dir;

    void Start()
    {
        self = GetComponent<Camera>();

        // Setup bullet caching and precalculate direction.
        SimulationEngine.OnPlaybackReady += PlaybackStarted;
        SimulationEngine.OnPlaybackCancelled += PlaybackTerminated;
        SimulationEngine.OnPlaybackComplete += PlaybackEnded;
    }

    void Update()
    {   
        // If we're following a valid bullet, use the bullet's transform, direction and our parent pivot's
        // position to correctly angle ourselves, otherwise just do as the main camera does.
        if (isFollowingBullet && targetBullet != null)
        {
            transform.parent.position = targetBullet.transform.position;
            transform.position = transform.parent.position - (dir.normalized * 5);
            transform.parent.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
        else
        {
            transform.parent.position = Camera.main.transform.position;
            transform.position = transform.parent.position;
            transform.parent.rotation = Camera.main.transform.rotation;
        }

        // Because we're using a target RenderTexture, manual rendering is required.
        self.Render();
    }

    void PlaybackStarted(GameObject bullet, Vector3 start, Vector3 end)
    {
        isFollowingBullet = true;
        targetBullet = bullet;
        dir = end - start;
    }

    void PlaybackEnded(GameObject bullet, Vector3 start, Vector3 end)
    {
        isFollowingBullet = false;
        targetBullet = null;
        dir = Vector3.zero;
    }
    void PlaybackTerminated()
    {
        isFollowingBullet = false;
        targetBullet = null;
        dir = Vector3.zero;
    }

    IEnumerator PauseBeforePerspDisable()
    {
        yield return new WaitForSeconds(2);

        transform.parent.position = targetBullet.transform.position;
        transform.position = transform.parent.position - (dir.normalized * 5);
        yield return null;
    }
}
