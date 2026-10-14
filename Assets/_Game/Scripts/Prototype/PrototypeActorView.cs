using UnityEngine;

namespace CrazyRubbish.Prototype
{
    // The parent stays on the simulation's contact point; only the artwork rocks while walking.
    public sealed class PrototypeActorView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer artwork, shadow;
        [SerializeField] private PrototypeAppearance appearance;
        [SerializeField] private int skin;
        private int facing = 1;
        private bool positioned;

        public SpriteRenderer Artwork => artwork;
        public int Facing => facing;

        public void Configure(PrototypeAppearance source, int character)
        {
            appearance = source;
            skin = character;
            artwork = new GameObject("Character").AddComponent<SpriteRenderer>();
            artwork.transform.SetParent(transform, false);
            artwork.sprite = appearance.characters[skin].Get(facing, 0);
            shadow = new GameObject("Contact Shadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(transform, false);
            shadow.sprite = appearance.marker;
            shadow.color = new Color32(19, 19, 18, 95);
            shadow.transform.localScale = new Vector3(0.55f / shadow.sprite.bounds.size.x, 0.13f / shadow.sprite.bounds.size.y, 1);
        }

        public void ResetMotion() { positioned = false; facing = 1; }

        public void Refresh(Vector2 position, int pose, bool paused, double gameSeconds)
        {
            Vector2 delta = position - (Vector2)transform.position;
            bool moved = positioned && delta.sqrMagnitude > 0.000001f && delta.sqrMagnitude < 1;
            if (moved && !paused)
                facing = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? (delta.x < 0 ? 2 : 3) : (delta.y < 0 ? 1 : 0);
            transform.position = new Vector3(position.x, position.y, 0);
            positioned = true;
            artwork.sprite = appearance.characters[skin].Get(facing, pose);
            float angle = moved && !paused && Application.isPlaying ? Mathf.Sin((float)gameSeconds * 12 + skin) * 2.5f : 0;
            artwork.transform.localRotation = Quaternion.Euler(0, 0, angle);
            artwork.sortingOrder = 1000 - Mathf.RoundToInt(position.y * 10) + 2;
            shadow.sortingOrder = artwork.sortingOrder - 1;
        }
    }
}
