package com.colorvision.xcviewer;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class OperationsConnectionPreferenceTest {
    @Test
    public void directIsTheSafeDefaultAndRelayMustBeExplicit() {
        assertEquals(OperationsConnectionPreference.DIRECT,
                OperationsConnectionPreference.normalize(null));
        assertEquals(OperationsConnectionPreference.DIRECT,
                OperationsConnectionPreference.normalize(""));
        assertEquals(OperationsConnectionPreference.DIRECT,
                OperationsConnectionPreference.normalize("automatic"));
        assertFalse(OperationsConnectionPreference.prefersRelay("direct"));
        assertTrue(OperationsConnectionPreference.prefersRelay("relay"));
    }

    @Test
    public void revokedProfileNeverFallsBackButTransportFailuresDo() {
        assertFalse(OperationsConnectionPreference.canFallbackAfter(
                "request_failed_403:unknown_or_revoked_device"));
        assertTrue(OperationsConnectionPreference.canFallbackAfter("timeout"));
        assertTrue(OperationsConnectionPreference.canFallbackAfter("failed_to_connect"));
        assertTrue(OperationsConnectionPreference.canFallbackAfter(null));
    }
}
