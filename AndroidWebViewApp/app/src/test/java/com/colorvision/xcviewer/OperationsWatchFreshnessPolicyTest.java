package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public class OperationsWatchFreshnessPolicyTest {
    private static final long NOW = 2_000_000_000_000L;

    @Test
    public void checkFreshnessHasExplicitTimeTrustBoundaries() {
        assertEquals(
                OperationsWatchFreshnessPolicy.Freshness.MISSING,
                OperationsWatchFreshnessPolicy.classify(0L, NOW));
        assertEquals(
                OperationsWatchFreshnessPolicy.Freshness.FRESH,
                OperationsWatchFreshnessPolicy.classify(NOW, NOW));
        assertEquals(
                OperationsWatchFreshnessPolicy.Freshness.FRESH,
                OperationsWatchFreshnessPolicy.classify(
                        NOW - OperationsWatchFreshnessPolicy.STALE_AFTER_MILLISECONDS,
                        NOW));
        assertEquals(
                OperationsWatchFreshnessPolicy.Freshness.STALE,
                OperationsWatchFreshnessPolicy.classify(
                        NOW - OperationsWatchFreshnessPolicy.STALE_AFTER_MILLISECONDS - 1L,
                        NOW));
        assertEquals(
                OperationsWatchFreshnessPolicy.Freshness.FRESH,
                OperationsWatchFreshnessPolicy.classify(
                        NOW + OperationsWatchFreshnessPolicy
                                .MAXIMUM_FUTURE_SKEW_MILLISECONDS,
                        NOW));
        assertEquals(
                OperationsWatchFreshnessPolicy.Freshness.FUTURE,
                OperationsWatchFreshnessPolicy.classify(
                        NOW + OperationsWatchFreshnessPolicy
                                .MAXIMUM_FUTURE_SKEW_MILLISECONDS + 1L,
                        NOW));
    }
}
