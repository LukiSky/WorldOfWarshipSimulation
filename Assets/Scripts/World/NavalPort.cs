using UnityEngine;

namespace Naval
{
    /// <summary>
    /// Friendly harbour: refuels, rearms and performs heavy repairs for ships that dock.
    /// Losing your harbour is a defeat condition in the escort and fleet battle modes.
    /// </summary>
    public class NavalPort : MonoBehaviour
    {
        public Team team = Team.Player;
        public string portName = "Port";
        public float serviceRadius = 90f;
        public float health = 5000f;
        public float maxHealth = 5000f;

        public Vector2 Position => new Vector2(transform.position.x, transform.position.y);
        public bool IsDestroyed => health <= 0f;

        public static NavalPort Create(Transform parent, Vector2 pos, Team team, string name)
        {
            var go = new GameObject("Port_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var p = go.AddComponent<NavalPort>();
            p.team = team;
            p.portName = name;
            p.BuildVisual();
            return p;
        }

        void BuildVisual()
        {
            // simple dock structure: a pier and a couple of warehouses
            var col = Teams.Color(team);
            MakeQuad("pier", new Vector2(0f, 0f), new Vector2(46f, 9f), new Color(0.30f, 0.27f, 0.24f), 0f, -6);
            MakeQuad("pier2", new Vector2(0f, 0f), new Vector2(9f, 46f), new Color(0.30f, 0.27f, 0.24f), 0f, -6);
            MakeQuad("shed1", new Vector2(-16f, 14f), new Vector2(14f, 10f), new Color(0.38f, 0.35f, 0.31f), 0f, -5);
            MakeQuad("shed2", new Vector2(16f, -14f), new Vector2(12f, 12f), new Color(0.36f, 0.33f, 0.30f), 0f, -5);
            MakeQuad("flag", new Vector2(0f, 0f), new Vector2(11f, 11f), col, 45f, -4);
        }

        void MakeQuad(string name, Vector2 offset, Vector2 size, Color color, float rot, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Square();
            sr.color = color;
            sr.sortingOrder = order;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        /// <summary>Called by ships that are inside the service radius.</summary>
        public void ServiceShip(Ship s, float dt)
        {
            if (IsDestroyed || s.team != team) return;
            s.Resources.Refuel(s.Stats.fuelCapacity * 0.09f * dt);
            s.Resources.Rearm(dt * 0.16f);
            s.Damage.PortRepair(dt);
        }

        public void TakeDamage(float amount)
        {
            if (IsDestroyed) return;
            health -= amount;
            if (health <= 0f)
            {
                health = 0f;
                GameEvents.RaiseMessage(portName + " has been destroyed!", Teams.Opponent(team));
                ParticleFX.Explosion(Position, 6f);
            }
        }
    }
}
