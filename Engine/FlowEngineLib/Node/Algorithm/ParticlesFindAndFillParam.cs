using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class ParticlesFindAndFillParam : AlgorithmImageParam
{
	public string ResultDataFileName { get; set; }

	public ParticlesMode ParticlesType { get; set; }

	public ParticlesFindAndFillParam(ParticlesMode particlesType, string outputFileName)
	{
		ParticlesType = particlesType;
		ResultDataFileName = outputFileName;
	}
}
