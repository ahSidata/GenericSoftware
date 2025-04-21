namespace EnergyAutomate.Definitions
{
    public class GrowattElement
    {
        #region Enums

        public enum ElementTypes
        {
            Adjustment,
            Calculation,
            Distribution
        }

        #endregion Enums

        #region Properties

        public ElementTypes ElementType { get; set; } = ElementTypes.Adjustment;
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool IsActive { get; set; }
        public string Name { get; set; } = string.Empty;

        #endregion Properties
    }
}
