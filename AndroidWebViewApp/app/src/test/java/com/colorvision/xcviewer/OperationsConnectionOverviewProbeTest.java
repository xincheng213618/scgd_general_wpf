package com.colorvision.xcviewer;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertTrue;

import org.json.JSONObject;
import org.junit.Test;

import java.util.concurrent.atomic.AtomicInteger;

public class OperationsConnectionOverviewProbeTest {
    private static final long NOW = 2_000_000_000_000L;

    @Test
    public void directPreferenceStopsAfterAWorkingDirectChannel() {
        AtomicInteger relayCalls = new AtomicInteger();

        OperationsConnectionOverviewProbe.Result result =
                OperationsConnectionOverviewProbe.run(
                        false,
                        NOW,
                        () -> { },
                        () -> {
                            relayCalls.incrementAndGet();
                            return new JSONObject();
                        });

        assertEquals(OperationsConnectionOverviewProbe.Channel.DIRECT, result.channel);
        assertEquals(0, relayCalls.get());
    }

    @Test
    public void directFailureFallsBackToAFreshRelayHost() throws Exception {
        JSONObject response = new JSONObject()
                .put("host", new JSONObject().put("signedAt", NOW / 1_000L));

        OperationsConnectionOverviewProbe.Result result =
                OperationsConnectionOverviewProbe.run(
                        false,
                        NOW,
                        () -> { throw new IllegalStateException("timeout"); },
                        () -> response);

        assertEquals(OperationsConnectionOverviewProbe.Channel.RELAY, result.channel);
        assertTrue(result.relayHostFresh);
        assertFalse(result.revoked);
    }

    @Test
    public void relayPreferenceKeepsAReachableRelayEvenWhileComputerIsWaiting() {
        AtomicInteger directCalls = new AtomicInteger();

        OperationsConnectionOverviewProbe.Result result =
                OperationsConnectionOverviewProbe.run(
                        true,
                        NOW,
                        () -> directCalls.incrementAndGet(),
                        JSONObject::new);

        assertEquals(OperationsConnectionOverviewProbe.Channel.RELAY, result.channel);
        assertFalse(result.relayHostFresh);
        assertEquals(0, directCalls.get());
    }

    @Test
    public void relayFailureFallsBackToAWorkingDirectChannel() {
        OperationsConnectionOverviewProbe.Result result =
                OperationsConnectionOverviewProbe.run(
                        true,
                        NOW,
                        () -> { },
                        () -> { throw new IllegalStateException("failed_to_connect"); });

        assertEquals(OperationsConnectionOverviewProbe.Channel.DIRECT, result.channel);
        assertFalse(result.revoked);
    }

    @Test
    public void twoFailuresNeverPretendThePreviousChannelIsStillActive() {
        OperationsConnectionOverviewProbe.Result result =
                OperationsConnectionOverviewProbe.run(
                        false,
                        NOW,
                        () -> { throw new IllegalStateException("timeout"); },
                        () -> { throw new IllegalStateException("failed_to_connect"); });

        assertEquals(OperationsConnectionOverviewProbe.Channel.UNAVAILABLE, result.channel);
        assertFalse(result.revoked);
        assertNotNull(result.directFailure);
        assertNotNull(result.relayFailure);
    }

    @Test
    public void revokedDirectAuthorizationDoesNotFallBackToAnotherChannel() {
        AtomicInteger relayCalls = new AtomicInteger();

        OperationsConnectionOverviewProbe.Result result =
                OperationsConnectionOverviewProbe.run(
                        false,
                        NOW,
                        () -> { throw new IllegalStateException("unknown_or_revoked_device"); },
                        () -> {
                            relayCalls.incrementAndGet();
                            return new JSONObject();
                        });

        assertTrue(result.revoked);
        assertEquals(0, relayCalls.get());
    }
}
