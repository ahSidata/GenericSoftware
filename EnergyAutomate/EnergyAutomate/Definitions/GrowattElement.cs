namespace EnergyAutomate.Definitions
{
    public class GrowattElement
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public enum ElementTypes
        {
            Adjustment,
            Calculation
        }

        public ElementTypes ElementType { get; set; } = ElementTypes.Adjustment;

        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
