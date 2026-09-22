using UnityEngine;

// Drops a newly placed track part into its place: fast at first, slowing into place, with a small
// settle at the end. It runs on game time, as the ball does, so the two stay in step at any time
// scale; and it moves every rendered frame, so it is smooth on 90 and 120 Hz screens.
//
// It used to move a fixed 0.25 per physics step: a constant speed that stopped dead, in visible
// physics-step jumps on fast screens, and 0.8 s from start to landing where this takes 0.5 s.
//
// After a revive, a part the ball is about to reach speeds up and lands just before the ball gets
// there: the ball rolls on the moment it is back, while the new path is still dropping, and it must
// never reach a part that is still in the air. Only then (HurryUntil) - in normal play parts land
// long before the ball comes, and the distance check below would needlessly hurry parts that a
// spiral or a hairpin puts close to the ball but far from it along the path.
public class MoveDown : MonoBehaviour
{
    // Game time until which parts hurry for the ball; set when a revive starts its new path.
    public static float HurryUntil;

    private const float DROP_HEIGHT = 10f;
    private const float DROP_SECONDS = 0.5f;
    // easeOutBack's overshoot constant: 0.5 dips the part about 1% of the drop (0.08) below its
    // place before it settles. The usual 1.70158 dips it 10%, a full unit here.
    private const float OVERSHOOT = 0.5f;
    // A hurried part lands when the ball has covered this share of the way to it.
    private const float LAND_BY = 0.7f;
    // The ball meets a part this far from the part's centre: half a part (2.5) plus the ball.
    private const float MEETING_DISTANCE = 1.25f + 0.25f;

    private static PlayerMovement ball;

    private Vector3 originalPos;
    private float progress; // 0 at the top, 1 landed
    private Transform previousPart = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // How many parts had to hurry, for MovementProbe.
    public static int HurriedLandings;
    private bool hurried;
#endif

    public void setPreviousPart(Transform previousPart)
    {
        this.previousPart = previousPart;
    }

    void Start()
    {
        originalPos = transform.position;
        transform.position = originalPos + new Vector3(0, DROP_HEIGHT, 0);
        if (ball == null)
            ball = FindAnyObjectByType<PlayerMovement>();
    }

    void Update()
    {
        float rate = 1f / DROP_SECONDS; // progress per second of game time
        float ballArrivesIn = secondsUntilTheBallArrives();
        if (ballArrivesIn <= 0.02f)
        {
            rate = float.MaxValue;
        }
        else if ((1f - progress) / rate > ballArrivesIn * LAND_BY)
        {
            rate = (1f - progress) / (ballArrivesIn * LAND_BY);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!hurried)
            {
                hurried = true;
                HurriedLandings++;
            }
#endif
        }
        progress = rate == float.MaxValue ? 1f : Mathf.Min(1f, progress + rate * Time.deltaTime);

        if (progress < 1f)
        {
            transform.position = originalPos + new Vector3(0, DROP_HEIGHT * (1f - easeOutBack(progress)), 0);
            return;
        }

        transform.position = originalPos;
        // gameObject.AddComponent<Destroyer>();
        if (transform.Find("PartStartBlock") != null)
            transform.Find("PartStartBlock").gameObject.SetActive(false);
        if (previousPart != null && previousPart.Find("PartEndBlock") != null)
            previousPart.Find("PartEndBlock").gameObject.SetActive(false);
        Destroy(this);
    }

    // Game-time seconds until the rolling ball reaches this part, judged in a straight line, which is
    // never longer than the way along the path, so a part never lands late.
    private float secondsUntilTheBallArrives()
    {
        if (Time.time >= HurryUntil || ball == null || !Utility.gameStarted || !Utility.camFollowPlayer || ball.speed <= 0.01f)
            return float.MaxValue;
        Vector3 d = originalPos - ball.transform.position;
        return Mathf.Max(0f, new Vector2(d.x, d.z).magnitude - MEETING_DISTANCE) / ball.speed;
    }

    // 0 to 1: fast at first, a little past 1 near the end, then back to exactly 1.
    private static float easeOutBack(float t)
    {
        float u = t - 1f;
        return 1f + (OVERSHOOT + 1f) * u * u * u + OVERSHOOT * u * u;
    }
}
