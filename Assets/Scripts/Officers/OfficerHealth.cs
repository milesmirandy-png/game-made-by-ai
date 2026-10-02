namespace Swat
{
    public class OfficerHealth : OperatorHealth
    {
        protected override void OnDamaged(DamageInfo info, float amount)
        {
            var squad = GetComponent<SquadAI>();
            if (squad != null) squad.NotifyHurt();
        }

        protected override void OnDowned()
        {
            var squad = GetComponent<SquadAI>();
            if (squad != null) squad.OnDowned();
        }

        protected override void OnRevived()
        {
            var squad = GetComponent<SquadAI>();
            if (squad != null) squad.OnRevived();
        }
    }
}
