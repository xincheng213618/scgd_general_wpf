package com.colorvision.xcviewer;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.Locale;

final class OperationsTriageFindingRevision {
    private OperationsTriageFindingRevision() {
    }

    static String findingId(String reportedId, String category, String title) {
        String normalized = safe(reportedId).trim();
        if (!normalized.isEmpty() && normalized.length() <= 128) {
            return normalized;
        }
        return "legacy-" + sha256(canonical(category, title)).substring(0, 24);
    }

    static String revision(
            String findingId,
            String severity,
            String category,
            String title,
            String summary,
            int evidenceCount,
            String latestAt) {
        String evidenceTimestamp = usesEvidenceTimestamp(category) ? safe(latestAt).trim() : "";
        return sha256(canonical(
                findingId,
                severity,
                category,
                title,
                summary,
                Integer.toString(Math.max(0, evidenceCount)),
                evidenceTimestamp));
    }

    private static boolean usesEvidenceTimestamp(String category) {
        String normalized = safe(category).trim().toLowerCase(Locale.ROOT);
        return "diagnostics".equals(normalized)
                || "failure-evidence".equals(normalized)
                || "message-service".equals(normalized);
    }

    private static String canonical(String... values) {
        StringBuilder canonical = new StringBuilder();
        for (String value : values) {
            String safeValue = safe(value).trim();
            canonical.append(safeValue.length()).append(':').append(safeValue).append(';');
        }
        return canonical.toString();
    }

    private static String sha256(String value) {
        try {
            byte[] digest = MessageDigest.getInstance("SHA-256")
                    .digest(value.getBytes(StandardCharsets.UTF_8));
            StringBuilder encoded = new StringBuilder(digest.length * 2);
            for (byte item : digest) {
                encoded.append(String.format(Locale.ROOT, "%02x", item & 0xff));
            }
            return encoded.toString();
        } catch (Exception ignored) {
            return String.format(Locale.ROOT, "%064x", value.hashCode());
        }
    }

    private static String safe(String value) {
        return value == null ? "" : value;
    }
}
