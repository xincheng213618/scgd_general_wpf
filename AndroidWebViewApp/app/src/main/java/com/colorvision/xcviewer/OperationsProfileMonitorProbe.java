package com.colorvision.xcviewer;

import org.json.JSONObject;

final class OperationsProfileMonitorProbe {
    private OperationsProfileMonitorProbe() {
    }

    static Result check(
            OperationsProfileRegistry.Profile profile,
            String deviceId,
            int connectTimeoutMilliseconds,
            int readTimeoutMilliseconds,
            long nowMilliseconds) {
        boolean relayFirst = OperationsConnectionPreference.prefersRelay(
                profile.connectionPreference);
        try {
            return relayFirst
                    ? checkRelay(profile, deviceId, connectTimeoutMilliseconds,
                            readTimeoutMilliseconds, nowMilliseconds)
                    : checkLocal(profile, deviceId, connectTimeoutMilliseconds,
                            readTimeoutMilliseconds);
        } catch (Exception firstException) {
            if (isRevoked(firstException)) {
                return Result.revoked();
            }
        }
        try {
            return relayFirst
                    ? checkLocal(profile, deviceId, connectTimeoutMilliseconds,
                            readTimeoutMilliseconds)
                    : checkRelay(profile, deviceId, connectTimeoutMilliseconds,
                            readTimeoutMilliseconds, nowMilliseconds);
        } catch (Exception secondException) {
            return isRevoked(secondException) ? Result.revoked() : Result.offline();
        }
    }

    private static Result checkLocal(
            OperationsProfileRegistry.Profile profile,
            String deviceId,
            int connectTimeoutMilliseconds,
            int readTimeoutMilliseconds) throws Exception {
        OperationsApiClient client = new OperationsApiClient(
                profile.endpoint,
                profile.certificatePin,
                deviceId,
                new OperationsDeviceIdentity(profile.hostId),
                connectTimeoutMilliseconds,
                readTimeoutMilliseconds);
        JSONObject response = client.get("/ops/v1/monitor");
        JSONObject monitor = response.optJSONObject("data");
        if (monitor == null) {
            throw new IllegalStateException("incomplete_live_monitor_response");
        }
        String attentionKey = OperationsMonitorClassifier.attentionKey(monitor);
        return Result.reachable(
                OperationsMonitorClassifier.watchState(
                        monitor, OperationsWatchHistory.STATE_ONLINE),
                attentionKey,
                OperationsMonitorClassifier.evidence(monitor, attentionKey));
    }

    private static Result checkRelay(
            OperationsProfileRegistry.Profile profile,
            String deviceId,
            int connectTimeoutMilliseconds,
            int readTimeoutMilliseconds,
            long nowMilliseconds) throws Exception {
        OperationsRelayApiClient client = new OperationsRelayApiClient(
                profile.hostId,
                deviceId,
                profile.certificatePin,
                new OperationsDeviceIdentity(profile.hostId),
                connectTimeoutMilliseconds,
                readTimeoutMilliseconds);
        JSONObject response = client.getSnapshot();
        JSONObject host = response.optJSONObject("host");
        if (host == null) {
            throw new IllegalStateException("incomplete_relay_snapshot");
        }
        boolean fresh = OperationsRelayPolicy.isHostFresh(
                host.optLong("signedAt", 0L), nowMilliseconds);
        if (!fresh) {
            return Result.reachable(
                    OperationsWatchHistory.STATE_REMOTE_WAITING,
                    "",
                    OperationsMonitorEvidenceRevision.Evidence.EMPTY);
        }
        JSONObject snapshot = host.optJSONObject("snapshot");
        JSONObject monitor = snapshot == null ? null : snapshot.optJSONObject("monitor");
        return monitor == null
                ? Result.reachable(
                        OperationsWatchHistory.STATE_REMOTE_ONLINE,
                        "",
                        OperationsMonitorEvidenceRevision.Evidence.EMPTY)
                : relayMonitorResult(monitor);
    }

    private static Result relayMonitorResult(JSONObject monitor) {
        String attentionKey = OperationsMonitorClassifier.attentionKey(monitor);
        return Result.reachable(
                OperationsMonitorClassifier.watchState(
                        monitor, OperationsWatchHistory.STATE_REMOTE_ONLINE),
                attentionKey,
                OperationsMonitorClassifier.evidence(monitor, attentionKey));
    }

    private static boolean isRevoked(Exception exception) {
        return !OperationsConnectionPreference.canFallbackAfter(
                exception == null ? null : exception.getMessage());
    }

    static final class Result {
        final boolean reachable;
        final boolean revoked;
        final String state;
        final String attentionKey;
        final OperationsMonitorEvidenceRevision.Evidence evidence;

        private Result(
                boolean reachable,
                boolean revoked,
                String state,
                String attentionKey,
                OperationsMonitorEvidenceRevision.Evidence evidence) {
            this.reachable = reachable;
            this.revoked = revoked;
            this.state = state;
            this.attentionKey = attentionKey;
            this.evidence = evidence;
        }

        static Result reachable(
                String state,
                String attentionKey,
                OperationsMonitorEvidenceRevision.Evidence evidence) {
            return new Result(true, false, state, attentionKey, evidence);
        }

        static Result offline() {
            return new Result(
                    false,
                    false,
                    OperationsWatchHistory.STATE_OFFLINE,
                    "",
                    OperationsMonitorEvidenceRevision.Evidence.EMPTY);
        }

        static Result revoked() {
            return new Result(
                    false,
                    true,
                    OperationsWatchHistory.STATE_REVOKED,
                    "",
                    OperationsMonitorEvidenceRevision.Evidence.EMPTY);
        }
    }
}
