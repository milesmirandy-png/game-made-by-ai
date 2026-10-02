namespace Swat
{
    public enum ObjectiveState { Pending, Active, Completed, Failed }

    // One mission objective at runtime, created from an ObjectiveDefinition
    // in the MissionData. ObjectiveTracker updates its state.
    public class Objective
    {
        public ObjectiveDefinition Definition { get; private set; }
        public ObjectiveType Type { get { return Definition.type; } }
        public string TargetId { get { return Definition.targetId; } }
        public bool Optional { get; private set; }
        public ObjectiveState State { get; set; }
        public int Progress { get; set; }
        public int Target { get; set; }
        public int Points { get { return Definition.points; } }
        public bool IsDone { get { return State == ObjectiveState.Completed || State == ObjectiveState.Failed; } }

        // Conditions hold until something breaks them; they're settled when the mission ends.
        public bool IsCondition
        {
            get
            {
                return Type == ObjectiveType.NoCivilianCasualties || Type == ObjectiveType.NoOfficerDown
                    || Type == ObjectiveType.AlarmNotTriggered || Type == ObjectiveType.TimeLimit;
            }
        }

        public string Label
        {
            get
            {
                string text = string.IsNullOrEmpty(Definition.description) ? Type.ToString() : Definition.description;
                if (Target > 1 && !IsCondition) text += "  (" + System.Math.Min(Progress, Target) + "/" + Target + ")";
                return text;
            }
        }

        public Objective(ObjectiveDefinition definition, bool optional)
        {
            Definition = definition;
            Optional = optional;
            State = ObjectiveState.Active;
            Target = definition.count;
        }

        public void Complete()
        {
            if (!IsDone) State = ObjectiveState.Completed;
        }

        public void Fail()
        {
            if (!IsDone) State = ObjectiveState.Failed;
        }
    }
}
