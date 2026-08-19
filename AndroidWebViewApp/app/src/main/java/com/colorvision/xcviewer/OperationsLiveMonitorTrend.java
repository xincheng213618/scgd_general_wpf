package com.colorvision.xcviewer;

import java.util.ArrayDeque;
import java.util.Deque;
import java.util.Iterator;

final class OperationsLiveMonitorTrend {
    static final int MAX_SAMPLES = 30;

    static final class Sample {
        final long capturedAtMilliseconds;
        final double cpuPercent;
        final double workingSetMb;
        final String uiState;
        final Long uiLatencyMilliseconds;
        final String flowPhase;
        final int alertCount;
        final boolean deviceHealthAvailable;
        final int deviceAttentionCount;

        Sample(long capturedAtMilliseconds,
               double cpuPercent,
               double workingSetMb,
               String uiState,
               Long uiLatencyMilliseconds,
               String flowPhase,
               int alertCount,
               boolean deviceHealthAvailable,
               int deviceAttentionCount) {
            this.capturedAtMilliseconds = Math.max(0, capturedAtMilliseconds);
            this.cpuPercent = Math.max(0, cpuPercent);
            this.workingSetMb = Math.max(0, workingSetMb);
            this.uiState = normalize(uiState, "unavailable");
            this.uiLatencyMilliseconds = uiLatencyMilliseconds == null
                    ? null : Math.max(0, uiLatencyMilliseconds);
            this.flowPhase = normalize(flowPhase, "unavailable");
            this.alertCount = Math.max(0, alertCount);
            this.deviceHealthAvailable = deviceHealthAvailable;
            this.deviceAttentionCount = Math.max(0, deviceAttentionCount);
        }
    }

    static final class Summary {
        final int sampleCount;
        final long startedAtMilliseconds;
        final long endedAtMilliseconds;
        final double averageCpuPercent;
        final double maximumCpuPercent;
        final double minimumWorkingSetMb;
        final double maximumWorkingSetMb;
        final Long maximumUiLatencyMilliseconds;
        final int slowUiSampleCount;
        final int unresponsiveUiSampleCount;
        final String firstFlowPhase;
        final String latestFlowPhase;
        final int flowPhaseTransitionCount;
        final int latestAlertCount;
        final int maximumAlertCount;
        final boolean deviceRecoveryTracked;
        final int initialDeviceAttentionCount;
        final boolean latestDeviceHealthAvailable;
        final int latestDeviceAttentionCount;
        final int consecutiveHealthyDeviceSamples;

        private Summary(int sampleCount,
                        long startedAtMilliseconds,
                        long endedAtMilliseconds,
                        double averageCpuPercent,
                        double maximumCpuPercent,
                        double minimumWorkingSetMb,
                        double maximumWorkingSetMb,
                        Long maximumUiLatencyMilliseconds,
                        int slowUiSampleCount,
                        int unresponsiveUiSampleCount,
                        String firstFlowPhase,
                        String latestFlowPhase,
                        int flowPhaseTransitionCount,
                        int latestAlertCount,
                        int maximumAlertCount,
                        boolean deviceRecoveryTracked,
                        int initialDeviceAttentionCount,
                        boolean latestDeviceHealthAvailable,
                        int latestDeviceAttentionCount,
                        int consecutiveHealthyDeviceSamples) {
            this.sampleCount = sampleCount;
            this.startedAtMilliseconds = startedAtMilliseconds;
            this.endedAtMilliseconds = endedAtMilliseconds;
            this.averageCpuPercent = averageCpuPercent;
            this.maximumCpuPercent = maximumCpuPercent;
            this.minimumWorkingSetMb = minimumWorkingSetMb;
            this.maximumWorkingSetMb = maximumWorkingSetMb;
            this.maximumUiLatencyMilliseconds = maximumUiLatencyMilliseconds;
            this.slowUiSampleCount = slowUiSampleCount;
            this.unresponsiveUiSampleCount = unresponsiveUiSampleCount;
            this.firstFlowPhase = firstFlowPhase;
            this.latestFlowPhase = latestFlowPhase;
            this.flowPhaseTransitionCount = flowPhaseTransitionCount;
            this.latestAlertCount = latestAlertCount;
            this.maximumAlertCount = maximumAlertCount;
            this.deviceRecoveryTracked = deviceRecoveryTracked;
            this.initialDeviceAttentionCount = initialDeviceAttentionCount;
            this.latestDeviceHealthAvailable = latestDeviceHealthAvailable;
            this.latestDeviceAttentionCount = latestDeviceAttentionCount;
            this.consecutiveHealthyDeviceSamples = consecutiveHealthyDeviceSamples;
        }

        static Summary empty() {
            return new Summary(0, 0, 0, 0, 0, 0, 0,
                    null, 0, 0, "unavailable", "unavailable", 0, 0, 0,
                    false, 0, false, 0, 0);
        }

        boolean deviceRecoveryPendingConfirmation() {
            return deviceRecoveryTracked
                    && latestDeviceHealthAvailable
                    && latestDeviceAttentionCount == 0
                    && consecutiveHealthyDeviceSamples == 1;
        }

        boolean deviceRecoveryConfirmed() {
            return deviceRecoveryTracked
                    && latestDeviceHealthAvailable
                    && latestDeviceAttentionCount == 0
                    && consecutiveHealthyDeviceSamples >= 2;
        }
    }

    private final Deque<Sample> samples = new ArrayDeque<>(MAX_SAMPLES);
    private Integer deviceRecoveryBaseline;

    void add(Sample sample) {
        if (sample == null) {
            return;
        }
        if (samples.size() == MAX_SAMPLES) {
            samples.removeFirst();
        }
        samples.addLast(sample);
    }

    void reset() {
        samples.clear();
        deviceRecoveryBaseline = null;
    }

    void trackDeviceRecovery(int attentionCount) {
        deviceRecoveryBaseline = attentionCount > 0 ? attentionCount : null;
    }

    int size() {
        return samples.size();
    }

    Summary summarize() {
        if (samples.isEmpty()) {
            return Summary.empty();
        }

        Sample first = samples.getFirst();
        Sample latest = samples.getLast();
        double cpuTotal = 0;
        double maximumCpu = 0;
        double minimumWorkingSet = Double.MAX_VALUE;
        double maximumWorkingSet = 0;
        Long maximumUiLatency = null;
        int slowUiSamples = 0;
        int unresponsiveUiSamples = 0;
        int flowTransitions = 0;
        int maximumAlerts = 0;
        String previousFlowPhase = null;
        Integer initialDeviceAttention = deviceRecoveryBaseline;

        for (Sample sample : samples) {
            cpuTotal += sample.cpuPercent;
            maximumCpu = Math.max(maximumCpu, sample.cpuPercent);
            minimumWorkingSet = Math.min(minimumWorkingSet, sample.workingSetMb);
            maximumWorkingSet = Math.max(maximumWorkingSet, sample.workingSetMb);
            if (sample.uiLatencyMilliseconds != null) {
                maximumUiLatency = maximumUiLatency == null
                        ? sample.uiLatencyMilliseconds
                        : Math.max(maximumUiLatency, sample.uiLatencyMilliseconds);
            }
            if ("slow".equals(sample.uiState)) {
                slowUiSamples++;
            } else if ("unresponsive".equals(sample.uiState)) {
                unresponsiveUiSamples++;
            }
            if (previousFlowPhase != null && !previousFlowPhase.equals(sample.flowPhase)) {
                flowTransitions++;
            }
            previousFlowPhase = sample.flowPhase;
            maximumAlerts = Math.max(maximumAlerts, sample.alertCount);
            if (initialDeviceAttention == null
                    && sample.deviceHealthAvailable
                    && sample.deviceAttentionCount > 0) {
                initialDeviceAttention = sample.deviceAttentionCount;
            }
        }

        int consecutiveHealthyDeviceSamples = 0;
        for (Iterator<Sample> iterator = samples.descendingIterator(); iterator.hasNext();) {
            Sample sample = iterator.next();
            if (!sample.deviceHealthAvailable || sample.deviceAttentionCount > 0) {
                break;
            }
            consecutiveHealthyDeviceSamples++;
        }

        return new Summary(
                samples.size(),
                first.capturedAtMilliseconds,
                latest.capturedAtMilliseconds,
                cpuTotal / samples.size(),
                maximumCpu,
                minimumWorkingSet == Double.MAX_VALUE ? 0 : minimumWorkingSet,
                maximumWorkingSet,
                maximumUiLatency,
                slowUiSamples,
                unresponsiveUiSamples,
                first.flowPhase,
                latest.flowPhase,
                flowTransitions,
                latest.alertCount,
                maximumAlerts,
                initialDeviceAttention != null && initialDeviceAttention > 0,
                initialDeviceAttention == null ? 0 : initialDeviceAttention,
                latest.deviceHealthAvailable,
                latest.deviceAttentionCount,
                consecutiveHealthyDeviceSamples);
    }

    private static String normalize(String value, String fallback) {
        if (value == null || value.trim().isEmpty()) {
            return fallback;
        }
        return value.trim();
    }
}
