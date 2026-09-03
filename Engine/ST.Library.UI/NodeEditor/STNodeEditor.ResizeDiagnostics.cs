#if COLORVISION_WINDOW_RESIZE_DIAGNOSTICS
using System;
using System.Diagnostics;

namespace ST.Library.UI.NodeEditor;

/// <summary>A copied, bounded render capture. Timestamps use Stopwatch.GetTimestamp(), not presentation time.</summary>
public sealed class STRenderDiagnosticCapture
{
	public int Capacity { get; internal set; }
	public STRenderDiagnosticSample[] Samples { get; internal set; }
	public long DroppedSamples { get; internal set; }
	public long UntilTimestamp { get; internal set; }
	public bool IsCapturing { get; internal set; }
	public bool IsDisposed { get; internal set; }
}

/// <summary>One synchronous OnRender call. A zero stage timestamp means that stage did not complete.</summary>
public struct STRenderDiagnosticSample
{
	public long StartTicks { get; internal set; }
	public long EnsureStartTicks { get; internal set; }
	public long EnsureEndTicks { get; internal set; }
	public long DrawEndTicks { get; internal set; }
	public long CopyStartTicks { get; internal set; }
	public long CopyEndTicks { get; internal set; }
	public long EndTicks { get; internal set; }
	public double LogicalWidth { get; internal set; }
	public double LogicalHeight { get; internal set; }
	public int PixelWidth { get; internal set; }
	public int PixelHeight { get; internal set; }
	public double DpiScaleX { get; internal set; }
	public double DpiScaleY { get; internal set; }
	public float CanvasScale { get; internal set; }
	public float CanvasOffsetX { get; internal set; }
	public float CanvasOffsetY { get; internal set; }
	public int NodesCount { get; internal set; }
	public bool BufferRecreated { get; internal set; }
	public bool Succeeded { get; internal set; }
}

public partial class STNodeEditor
{
	private const int ResizeDiagnosticCapacity = 2048;
	private STRenderDiagnosticSample[] m_resize_diagnostic_samples;
	private int m_resize_diagnostic_count;
	private long m_resize_diagnostic_dropped;
	private long m_resize_diagnostic_until;

	/// <summary>Extends a capture's absolute QPC deadline without clearing earlier samples. Call on the editor's Dispatcher.</summary>
	public void BeginResizeDiagnosticCapture(long untilTimestamp)
	{
		VerifyAccess();
		if (m_disposed || untilTimestamp <= Stopwatch.GetTimestamp())
			return;
		m_resize_diagnostic_samples ??= new STRenderDiagnosticSample[ResizeDiagnosticCapacity];
		m_resize_diagnostic_until = Math.Max(m_resize_diagnostic_until, untilTimestamp);
	}

	/// <summary>Stops sampling but preserves collected samples. Call on the editor's Dispatcher.</summary>
	public void StopResizeDiagnosticCapture()
	{
		VerifyAccess();
		m_resize_diagnostic_until = 0;
	}

	/// <summary>Copies the capture for explicit export. Does not trigger rendering. Call on the editor's Dispatcher.</summary>
	public STRenderDiagnosticCapture GetResizeDiagnosticCapture()
	{
		VerifyAccess();
		var samples = m_resize_diagnostic_count == 0
			? Array.Empty<STRenderDiagnosticSample>()
			: new STRenderDiagnosticSample[m_resize_diagnostic_count];
		if (samples.Length != 0)
			Array.Copy(m_resize_diagnostic_samples, samples, samples.Length);
		return new STRenderDiagnosticCapture
		{
			Capacity = ResizeDiagnosticCapacity,
			Samples = samples,
			DroppedSamples = m_resize_diagnostic_dropped,
			UntilTimestamp = m_resize_diagnostic_until,
			IsCapturing = !m_disposed && m_resize_diagnostic_until > 0 && Stopwatch.GetTimestamp() < m_resize_diagnostic_until,
			IsDisposed = m_disposed
		};
	}

	private bool TryBeginResizeDiagnosticSample(out STRenderDiagnosticSample sample)
	{
		sample = default;
		if (m_disposed || m_resize_diagnostic_samples == null || m_resize_diagnostic_until == 0)
			return false;
		long now = Stopwatch.GetTimestamp();
		if (now >= m_resize_diagnostic_until)
		{
			m_resize_diagnostic_until = 0;
			return false;
		}
		if (m_resize_diagnostic_count == ResizeDiagnosticCapacity)
		{
			m_resize_diagnostic_dropped++;
			return false;
		}
		sample = new STRenderDiagnosticSample
		{
			StartTicks = now,
			CanvasScale = _CanvasScale,
			CanvasOffsetX = _CanvasOffsetX,
			CanvasOffsetY = _CanvasOffsetY,
			NodesCount = _Nodes.Count
		};
		return true;
	}

	private void CompleteResizeDiagnosticSample(ref STRenderDiagnosticSample sample)
	{
		sample.EndTicks = Stopwatch.GetTimestamp();
		if (m_disposed || m_resize_diagnostic_samples == null)
			return;
		if (m_resize_diagnostic_count < ResizeDiagnosticCapacity)
			m_resize_diagnostic_samples[m_resize_diagnostic_count++] = sample;
		else
			m_resize_diagnostic_dropped++;
	}

	private void DisposeResizeDiagnosticCapture()
	{
		m_resize_diagnostic_until = 0;
		m_resize_diagnostic_samples = null;
		m_resize_diagnostic_count = 0;
		m_resize_diagnostic_dropped = 0;
	}
}
#endif
