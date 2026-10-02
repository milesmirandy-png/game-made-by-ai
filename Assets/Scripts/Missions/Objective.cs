namespace Swat
{
    public enum ObjectiveState { Pending, Active, Completed, Failed }

    // One line on the objectives list. Objectives with a target count show progress, e.g. "(2/5)".
    public class Objective
    {
        public string Title { get; private set; }
        public int Points { get; private set; }
        public ObjectiveState State { get; set; }
        public int Progress { get; private set; }
        public int Target { get; private set; }
        public string Label { get; private set; }

        public Objective(string title, int points, int target = 0, ObjectiveState state = ObjectiveState.Active)
        {
            Title = title;
            Points = points;
            Target = target;
            State = state;
            RefreshLabel();
        }

        public bool IsDone { get { return State == ObjectiveState.Completed || State == ObjectiveState.Failed; } }

        public void SetProgress(int value)
        {
            if (value == Progress) return;
            Progress = value;
            RefreshLabel();
        }

        void RefreshLabel()
        {
            Label = Target > 0 ? Title + "  (" + Progress + "/" + Target + ")" : Title;
        }
    }
}
