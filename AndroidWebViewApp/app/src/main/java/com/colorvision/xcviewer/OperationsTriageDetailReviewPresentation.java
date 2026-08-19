package com.colorvision.xcviewer;

final class OperationsTriageDetailReviewPresentation {
    static final String SURFACE_RECENT_EVENTS = "recent-events";
    static final String SURFACE_DEVICE_HEALTH = "device-health";
    static final String SURFACE_MESSAGE_CHANNEL = "message-channel";
    static final String SURFACE_SERVICE_HEALTH = "service-health";
    static final String SURFACE_FAILURE_EVIDENCE = "failure-evidence";
    static final String SURFACE_PERFORMANCE = "performance";
    static final String SURFACE_APPLICATION = "application";

    private OperationsTriageDetailReviewPresentation() {
    }

    static ViewModel from(
            OperationsTriagePresentation.Finding finding,
            boolean inFlight) {
        return from(finding, inFlight, "");
    }

    static ViewModel from(
            OperationsTriagePresentation.Finding finding,
            boolean inFlight,
            String updatingEvidenceLabel) {
        if (finding == null) {
            return new ViewModel(false, "", "", false, false);
        }
        if (updatingEvidenceLabel != null && !updatingEvidenceLabel.isEmpty()) {
            return new ViewModel(
                    true,
                    updatingEvidenceLabel,
                    "正在执行受控维护或刷新状态；完成前不能复核可能变化的证据",
                    false,
                    finding.acknowledged);
        }
        if (inFlight) {
            return new ViewModel(
                    true,
                    "正在核对最新问题证据…",
                    "正在重新读取电脑端最新问题证据，完成前不会改变复核状态",
                    false,
                    finding.acknowledged);
        }
        if (finding.acknowledged) {
            return new ViewModel(
                    true,
                    "撤销此问题复核",
                    "撤销本机对当前证据的复核，恢复为待复核",
                    true,
                    true);
        }
        return new ViewModel(
                true,
                "标记此问题已复核",
                "重新核对电脑端最新证据后标记为已在此手机复核；"
                        + "电脑状态不会改变，新证据会自动恢复为待复核",
                true,
                false);
    }

    static String surfaceFor(OperationsTriagePresentation.Finding finding) {
        if (finding == null) {
            return "";
        }
        if ("diagnostics".equals(finding.category)) {
            return SURFACE_RECENT_EVENTS;
        }
        if ("message-service".equals(finding.category)) {
            return SURFACE_RECENT_EVENTS;
        }
        if ("devices".equals(finding.category)) {
            return SURFACE_DEVICE_HEALTH;
        }
        if ("message-channel".equals(finding.category)) {
            return SURFACE_MESSAGE_CHANNEL;
        }
        if ("services".equals(finding.category)) {
            return SURFACE_SERVICE_HEALTH;
        }
        if ("failure-evidence".equals(finding.category)) {
            return SURFACE_FAILURE_EVIDENCE;
        }
        if ("performance".equals(finding.category)) {
            return SURFACE_PERFORMANCE;
        }
        if ("desktop".equals(finding.category)) {
            return SURFACE_APPLICATION;
        }
        return "";
    }

    static OperationsTriagePresentation.Finding findCurrent(
            OperationsTriagePresentation.ViewModel model,
            OperationsTriagePresentation.Finding reference) {
        if (model == null || reference == null) {
            return null;
        }
        for (OperationsTriagePresentation.Finding finding : model.findings) {
            if (reference.findingId.equals(finding.findingId)) {
                return finding;
            }
        }
        return null;
    }

    static boolean requiresEvidenceRefresh(
            OperationsTriagePresentation.Finding latest,
            boolean acknowledging,
            boolean hasUnseenEvidence) {
        return acknowledging
                && latest != null
                && !latest.acknowledged
                && hasUnseenEvidence;
    }

    static final class ViewModel {
        final boolean visible;
        final String label;
        final String contentDescription;
        final boolean enabled;
        final boolean acknowledged;

        ViewModel(
                boolean visible,
                String label,
                String contentDescription,
                boolean enabled,
                boolean acknowledged) {
            this.visible = visible;
            this.label = label;
            this.contentDescription = contentDescription;
            this.enabled = enabled;
            this.acknowledged = acknowledged;
        }
    }
}
