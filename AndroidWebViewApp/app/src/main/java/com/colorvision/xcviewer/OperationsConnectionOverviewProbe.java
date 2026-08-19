package com.colorvision.xcviewer;

import org.json.JSONObject;

final class OperationsConnectionOverviewProbe {
    enum Channel {
        CHECKING,
        DIRECT,
        RELAY,
        UNAVAILABLE
    }

    private OperationsConnectionOverviewProbe() {
    }

    static Result checking() {
        return new Result(Channel.CHECKING, false, false, null, null);
    }

    static Result run(
            boolean relayFirst,
            long nowMilliseconds,
            DirectCheck directCheck,
            RelayCheck relayCheck) {
        if (relayFirst) {
            Exception relayFailure;
            try {
                return relayResult(relayCheck.run(), nowMilliseconds);
            } catch (Exception ex) {
                relayFailure = ex;
                if (!canFallbackAfter(ex)) {
                    return revoked(null, relayFailure);
                }
            }
            try {
                directCheck.run();
                return direct();
            } catch (Exception directFailure) {
                return canFallbackAfter(directFailure)
                        ? unavailable(directFailure, relayFailure)
                        : revoked(directFailure, relayFailure);
            }
        }

        Exception directFailure;
        try {
            directCheck.run();
            return direct();
        } catch (Exception ex) {
            directFailure = ex;
            if (!canFallbackAfter(ex)) {
                return revoked(directFailure, null);
            }
        }
        try {
            return relayResult(relayCheck.run(), nowMilliseconds);
        } catch (Exception relayFailure) {
            return canFallbackAfter(relayFailure)
                    ? unavailable(directFailure, relayFailure)
                    : revoked(directFailure, relayFailure);
        }
    }

    private static Result relayResult(JSONObject response, long nowMilliseconds) {
        if (response == null) {
            throw new IllegalStateException("incomplete_relay_response");
        }
        JSONObject host = response.optJSONObject("host");
        boolean hostFresh = host != null && OperationsRelayPolicy.isHostFresh(
                host.optLong("signedAt", 0L), nowMilliseconds);
        return new Result(Channel.RELAY, hostFresh, false, null, null);
    }

    private static Result direct() {
        return new Result(Channel.DIRECT, true, false, null, null);
    }

    private static Result unavailable(Exception directFailure, Exception relayFailure) {
        return new Result(Channel.UNAVAILABLE, false, false,
                directFailure, relayFailure);
    }

    private static Result revoked(Exception directFailure, Exception relayFailure) {
        return new Result(Channel.UNAVAILABLE, false, true,
                directFailure, relayFailure);
    }

    private static boolean canFallbackAfter(Exception exception) {
        return OperationsConnectionPreference.canFallbackAfter(
                exception == null ? null : exception.getMessage());
    }

    interface DirectCheck {
        void run() throws Exception;
    }

    interface RelayCheck {
        JSONObject run() throws Exception;
    }

    static final class Result {
        final Channel channel;
        final boolean relayHostFresh;
        final boolean revoked;
        final Exception directFailure;
        final Exception relayFailure;

        Result(
                Channel channel,
                boolean relayHostFresh,
                boolean revoked,
                Exception directFailure,
                Exception relayFailure) {
            this.channel = channel;
            this.relayHostFresh = relayHostFresh;
            this.revoked = revoked;
            this.directFailure = directFailure;
            this.relayFailure = relayFailure;
        }
    }
}
