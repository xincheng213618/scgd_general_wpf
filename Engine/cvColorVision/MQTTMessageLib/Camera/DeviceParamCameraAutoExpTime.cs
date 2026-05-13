using CVCommCore.CVCamera;
using MQTTMessageLib.Algorithm;

namespace MQTTMessageLib.Camera;

public class DeviceParamCameraAutoExpTime : ParamDicBase
{
	private bool _AutoExpFlag;

	private float _AutoExpTimeBegin;

	private float _AutoExpSyncFreq;

	private float _AutoExpSaturation;

	private uint _AutoExpSatMaxAD;

	private float _AutoExpMaxPecentage;

	private float _AutoExpSatDev;

	private float _MaxExpTime;

	private float _MinExpTime;

	private float _BurstThreshold;

	public bool autoExpFlag
	{
		get
		{
			return GetValue(_AutoExpFlag, "autoExpFlag");
		}
		set
		{
			SetProperty(ref _AutoExpFlag, value, "autoExpFlag");
		}
	}

	public float autoExpTimeBegin
	{
		get
		{
			return GetValue(_AutoExpTimeBegin, "autoExpTimeBegin");
		}
		set
		{
			SetProperty(ref _AutoExpTimeBegin, value, "autoExpTimeBegin");
		}
	}

	public float autoExpSyncFreq
	{
		get
		{
			return GetValue(_AutoExpSyncFreq, "autoExpSyncFreq");
		}
		set
		{
			SetProperty(ref _AutoExpSyncFreq, value, "autoExpSyncFreq");
		}
	}

	public float autoExpSaturation
	{
		get
		{
			return GetValue(_AutoExpSaturation, "autoExpSaturation");
		}
		set
		{
			SetProperty(ref _AutoExpSaturation, value, "autoExpSaturation");
		}
	}

	public uint autoExpSatMaxAD
	{
		get
		{
			return GetValue(_AutoExpSatMaxAD, "autoExpSatMaxAD");
		}
		set
		{
			SetProperty(ref _AutoExpSatMaxAD, value, "autoExpSatMaxAD");
		}
	}

	public float autoExpMaxPecentage
	{
		get
		{
			return GetValue(_AutoExpMaxPecentage, "autoExpMaxPecentage");
		}
		set
		{
			SetProperty(ref _AutoExpMaxPecentage, value, "autoExpMaxPecentage");
		}
	}

	public float autoExpSatDev
	{
		get
		{
			return GetValue(_AutoExpSatDev, "autoExpSatDev");
		}
		set
		{
			SetProperty(ref _AutoExpSatDev, value, "autoExpSatDev");
		}
	}

	public float maxExpTime
	{
		get
		{
			return GetValue(_MaxExpTime, "maxExpTime");
		}
		set
		{
			SetProperty(ref _MaxExpTime, value, "maxExpTime");
		}
	}

	public float minExpTime
	{
		get
		{
			return GetValue(_MinExpTime, "minExpTime");
		}
		set
		{
			SetProperty(ref _MinExpTime, value, "minExpTime");
		}
	}

	public float burstThreshold
	{
		get
		{
			return GetValue(_BurstThreshold, "burstThreshold");
		}
		set
		{
			SetProperty(ref _BurstThreshold, value, "burstThreshold");
		}
	}

	public SysAutoExpTimeCfg ToSysCfg()
	{
		return new SysAutoExpTimeCfg
		{
			AutoExpFlag = autoExpFlag,
			AutoExpMaxPecentage = autoExpMaxPecentage,
			AutoExpSatDev = autoExpSatDev,
			AutoExpSatMaxAD = autoExpSatMaxAD,
			AutoExpSaturation = autoExpSaturation,
			AutoExpSyncFreq = autoExpSyncFreq,
			AutoExpTimeBegin = autoExpTimeBegin,
			BurstThreshold = burstThreshold,
			MaxExpTime = maxExpTime,
			MinExpTime = minExpTime
		};
	}

	public static DeviceParamCameraAutoExpTime Parse(SysAutoExpTimeCfg expTimeCfg)
	{
		return new DeviceParamCameraAutoExpTime
		{
			autoExpFlag = expTimeCfg.AutoExpFlag,
			autoExpMaxPecentage = expTimeCfg.AutoExpMaxPecentage,
			autoExpSatDev = expTimeCfg.AutoExpSatDev,
			autoExpSatMaxAD = expTimeCfg.AutoExpSatMaxAD,
			autoExpSaturation = expTimeCfg.AutoExpSaturation,
			autoExpSyncFreq = expTimeCfg.AutoExpSyncFreq,
			autoExpTimeBegin = expTimeCfg.AutoExpTimeBegin,
			burstThreshold = expTimeCfg.BurstThreshold,
			maxExpTime = expTimeCfg.MaxExpTime,
			minExpTime = expTimeCfg.MinExpTime
		};
	}
}
