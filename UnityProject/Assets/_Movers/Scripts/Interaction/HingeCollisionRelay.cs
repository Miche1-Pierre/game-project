using UnityEngine;

namespace Movers
{
    // Added by HingedPanel to its "_Hinge" object. Never placed by hand, so it stays out of the
    // Add Component menu; it has its own file so Unity knows its script and the inspector shows
    // it by name during Play.
    //
    // Unity reports a collision to the GameObject that owns the Rigidbody, not to the child
    // collider that was touched. Before it was hinged, a pane of glass was a static collider and
    // heard its own impacts; once hinged, its hinge owns the body. So the hinge passes each new
    // contact on to the piece that was actually hit, and a thrown chair still breaks the glass.
    // Enter only: GlassPane and Breakable only listen for Enter.
    //
    // Except the hits the panel makes itself. A moving kinematic body lends its own speed to the
    // contact, so without this a veranda door opened into a chair would measure its own swing as
    // an impact and break its own glass. While the panel swings, a contact is passed on only when
    // it is faster than anything the swing alone could produce: a mug thrown at a moving door
    // still counts, the door bumping into a chair does not.
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class HingeCollisionRelay : MonoBehaviour
    {
        // m/s on top of the panel's own edge speed before a hit during a swing counts.
        const float SelfHitMargin = 0.5f;

        // The panel this hinge turns. Set by HingedPanel when it builds the hinge.
        internal HingedPanel owner;

        void OnCollisionEnter(Collision c)
        {
            if (c.contactCount == 0) return;
            ContactPoint contact = c.GetContact(0);
            Collider hit = contact.thisCollider;
            if (hit == null || hit.gameObject == gameObject) return;

            if (owner != null && owner.SwungRecently)
            {
                // Same measure as Breakable and GlassPane use: approach speed along the normal.
                float approach = Mathf.Abs(Vector3.Dot(c.relativeVelocity, contact.normal));
                if (approach <= owner.EdgeSpeed + SelfHitMargin) return;
            }
            hit.gameObject.SendMessage("OnCollisionEnter", c, SendMessageOptions.DontRequireReceiver);
        }
    }
}
