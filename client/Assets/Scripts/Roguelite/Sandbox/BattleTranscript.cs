using System.Collections.Generic;
using System.Text;
using Pets.Roguelite.Simulation;

namespace Pets.Roguelite.Sandbox
{
    /// <summary>Renders a Step-event log as human-readable text, grouped by Step number.</summary>
    public static class BattleTranscript
    {
        public static string Render(List<StepEvent> log)
        {
            var sb = new StringBuilder();
            int currentStep = -1;

            foreach (var evt in log)
            {
                if (evt.Kind == StepEventKind.BattleEnd)
                {
                    sb.AppendLine();
                    sb.AppendLine(evt.ToString());
                    continue;
                }

                if (evt.StepNumber != currentStep)
                {
                    currentStep = evt.StepNumber;
                    sb.AppendLine();
                    sb.AppendLine($"--- Step {currentStep} ---");
                }

                sb.AppendLine(evt.ToString());
            }

            return sb.ToString();
        }
    }
}
