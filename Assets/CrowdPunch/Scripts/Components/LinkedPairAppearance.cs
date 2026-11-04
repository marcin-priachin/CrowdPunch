using Unity.Entities;
using Unity.Mathematics;

namespace CrowdPunch.Components
{
    public struct LinkedPairAppearance : IComponentData
    {
        public int StreamCount;
        public float StreamWidth, StreamSpread, StreamWavelength, GlowWidth;
        public float GroundFlowSpeed, FlightFlowSpeed;
        public float4 GroundCore, GroundHighlight, GroundGlow;
        public float4 FlightCore, FlightHighlight, FlightGlow;
        public float4 SparkColor;
        public float SparkRate, SparkLifetime, SparkLength, SparkTravel;
    }
}
