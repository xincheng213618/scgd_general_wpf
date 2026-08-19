package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;

public class OperationsLiveMonitorTrendTest {
    @Test
    public void retainsOnlyLatestThirtySamplesAndCalculatesBoundedTrend() {
        OperationsLiveMonitorTrend trend = new OperationsLiveMonitorTrend();
        for (int index = 0; index < 35; index++) {
            trend.add(new OperationsLiveMonitorTrend.Sample(
                    1_000L * index,
                    index,
                    100 + index,
                    index == 33 ? "slow" : index == 34 ? "unresponsive" : "responsive",
                     (long) index,
                     index < 20 ? "running" : "idle",
                     index % 4,
                     true,
                     index < 34 ? 2 : 0));
        }

        OperationsLiveMonitorTrend.Summary summary = trend.summarize();

        assertEquals(30, trend.size());
        assertEquals(30, summary.sampleCount);
        assertEquals(5_000L, summary.startedAtMilliseconds);
        assertEquals(34_000L, summary.endedAtMilliseconds);
        assertEquals(19.5, summary.averageCpuPercent, 0.001);
        assertEquals(34, summary.maximumCpuPercent, 0.001);
        assertEquals(105, summary.minimumWorkingSetMb, 0.001);
        assertEquals(134, summary.maximumWorkingSetMb, 0.001);
        assertEquals(Long.valueOf(34), summary.maximumUiLatencyMilliseconds);
        assertEquals(1, summary.slowUiSampleCount);
        assertEquals(1, summary.unresponsiveUiSampleCount);
        assertEquals("running", summary.firstFlowPhase);
        assertEquals("idle", summary.latestFlowPhase);
        assertEquals(1, summary.flowPhaseTransitionCount);
        assertEquals(2, summary.latestAlertCount);
        assertEquals(3, summary.maximumAlertCount);
        assertEquals(2, summary.initialDeviceAttentionCount);
        assertEquals(0, summary.latestDeviceAttentionCount);
        assertEquals(1, summary.consecutiveHealthyDeviceSamples);
    }

    @Test
    public void resetRemovesTheEntireInMemorySession() {
        OperationsLiveMonitorTrend trend = new OperationsLiveMonitorTrend();
        trend.add(new OperationsLiveMonitorTrend.Sample(
                -1, -2, -3, "", null, "", -4, false, -5));
        trend.reset();

        OperationsLiveMonitorTrend.Summary summary = trend.summarize();

        assertEquals(0, trend.size());
        assertEquals(0, summary.sampleCount);
        assertEquals("unavailable", summary.latestFlowPhase);
        assertNull(summary.maximumUiLatencyMilliseconds);
    }

    @Test
    public void deviceRecoveryNeedsTwoConsecutiveHealthySamples() {
        OperationsLiveMonitorTrend trend = new OperationsLiveMonitorTrend();
        trend.trackDeviceRecovery(2);
        trend.add(new OperationsLiveMonitorTrend.Sample(
                1_000, 0, 100, "responsive", 10L, "idle", 0, true, 0));

        OperationsLiveMonitorTrend.Summary pending = trend.summarize();

        assertEquals(1, pending.consecutiveHealthyDeviceSamples);
        assertEquals(true, pending.deviceRecoveryPendingConfirmation());
        assertEquals(false, pending.deviceRecoveryConfirmed());

        trend.add(new OperationsLiveMonitorTrend.Sample(
                11_000, 0, 100, "responsive", 10L, "idle", 0, true, 0));

        OperationsLiveMonitorTrend.Summary confirmed = trend.summarize();

        assertEquals(2, confirmed.consecutiveHealthyDeviceSamples);
        assertEquals(false, confirmed.deviceRecoveryPendingConfirmation());
        assertEquals(true, confirmed.deviceRecoveryConfirmed());

        trend.add(new OperationsLiveMonitorTrend.Sample(
                21_000, 0, 100, "responsive", 10L, "idle", 0, true, 1));

        OperationsLiveMonitorTrend.Summary relapsed = trend.summarize();

        assertEquals(0, relapsed.consecutiveHealthyDeviceSamples);
        assertEquals(false, relapsed.deviceRecoveryConfirmed());
    }
}
