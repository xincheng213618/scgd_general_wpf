using System;
using FlowEngineLib.Base;

namespace FlowEngineLib;

internal class LoopDataModel
{
	public float m_cur_val;

	public float m_begin_val;

	public float m_end_val;

	public float m_step_val;

	public int m_cur_step;

	public DateTime startTime;

	public CVStartCFC startCFC;

	public LoopDataModel(float begin_val, float end_val, float step_val, CVStartCFC action)
	{
		m_begin_val = begin_val;
		m_end_val = end_val;
		m_step_val = step_val;
		startCFC = action;
		m_cur_val = begin_val;
		m_cur_step = 1;
		startTime = DateTime.Now;
	}

	public void Next()
	{
		m_cur_val += m_step_val;
		m_cur_step++;
		startTime = DateTime.Now;
	}

	public bool IsEnd()
	{
		if (!(m_cur_val >= m_end_val))
		{
			return m_cur_val + m_step_val > m_end_val;
		}
		return true;
	}
}
