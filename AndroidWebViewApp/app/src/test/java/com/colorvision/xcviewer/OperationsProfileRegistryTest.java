package com.colorvision.xcviewer;

import org.json.JSONArray;
import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertThrows;
import static org.junit.Assert.assertTrue;

public class OperationsProfileRegistryTest {
    private static final String PIN = "a".repeat(64);

    @Test
    public void legacyProfileMigratesWithoutLosingIndependentState() {
        OperationsProfileRegistry.State migrated = OperationsProfileRegistry.fromLegacy(
                "https://192.168.1.20:5800",
                PIN,
                "host_1",
                OperationsConnectionPreference.RELAY,
                false,
                "history",
                "task_1",
                OperationsRelayPolicy.CAPABILITY_SHOW_WINDOW,
                "idem_1");

        OperationsProfileRegistry.State parsed = OperationsProfileRegistry.parse(
                OperationsProfileRegistry.serialize(migrated));
        OperationsProfileRegistry.Profile active = parsed.active();
        assertNotNull(active);
        assertEquals("host_1", active.hostId);
        assertEquals(OperationsConnectionPreference.RELAY, active.connectionPreference);
        assertEquals("history", active.watchHistory);
        assertEquals("task_1", active.relayTaskId);
        assertEquals(OperationsRelayPolicy.CAPABILITY_SHOW_WINDOW,
                active.relayTaskCapability);
        assertEquals("idem_1", active.relayTaskIdempotency);
    }

    @Test
    public void switchingKeepsConnectionHistoryAndRecentTaskPerComputer() {
        OperationsProfileRegistry.State state = OperationsProfileRegistry.empty()
                .upsert("https://192.168.1.21:5800", PIN, "host_1")
                .updateConnectionPreference(OperationsConnectionPreference.RELAY)
                .updateWatchHistory("history_1", 101L)
                .updateRelayTask("task_1", OperationsRelayPolicy.CAPABILITY_SHOW_WINDOW,
                        "idem_1")
                .upsert("https://192.168.1.22:5800", "b".repeat(64), "host_2")
                .updateWatchHistory("history_2", 202L);
        state = OperationsProfileRegistry.parse(OperationsProfileRegistry.serialize(state));

        assertEquals("host_2", state.active().hostId);
        assertEquals(OperationsConnectionPreference.DIRECT,
                state.active().connectionPreference);
        assertEquals("history_2", state.active().watchHistory);
        assertEquals(202L, state.active().watchCheckedAt);
        assertEquals("", state.active().relayTaskId);

        OperationsProfileRegistry.Profile first = state.select("host_1").active();
        assertEquals(OperationsConnectionPreference.RELAY, first.connectionPreference);
        assertEquals("history_1", first.watchHistory);
        assertEquals(101L, first.watchCheckedAt);
        assertEquals("task_1", first.relayTaskId);
        assertEquals("idem_1", first.relayTaskIdempotency);
    }

    @Test
    public void removalAndRevocationSelectTheNextUsableComputer() {
        OperationsProfileRegistry.State state = OperationsProfileRegistry.empty()
                .upsert("https://192.168.1.21:5800", PIN, "host_1")
                .upsert("https://192.168.1.22:5800", "b".repeat(64), "host_2")
                .updateWatchHistory("history_2", 303L);

        OperationsProfileRegistry.State revoked = state.revoke("host_2");
        assertEquals("host_1", revoked.activeHostId);
        assertEquals(1, revoked.usableCount());
        assertTrue(revoked.profiles.get(1).revoked);
        assertEquals(303L, revoked.profiles.get(1).watchCheckedAt);
        assertEquals("host_1", revoked.select("host_2").activeHostId);

        OperationsProfileRegistry.State removed = state.remove("host_2");
        assertEquals("host_1", removed.activeHostId);
        assertEquals(1, removed.profiles.size());
        assertTrue(removed.remove("host_1").profiles.isEmpty());
    }

    @Test
    public void parserDropsInvalidAndDuplicateProfiles() throws Exception {
        JSONObject valid = profile("host_1", "https://192.168.1.21:5800", PIN);
        JSONObject duplicate = profile("host_1", "https://192.168.1.99:5800", PIN);
        JSONObject insecure = profile("host_2", "http://192.168.1.22:5800", PIN);
        JSONObject invalidPin = profile("host_3", "https://192.168.1.23:5800", "bad");
        String serialized = new JSONObject()
                .put("version", 1)
                .put("activeHostId", "missing")
                .put("profiles", new JSONArray()
                        .put(valid)
                        .put(duplicate)
                        .put(insecure)
                        .put(invalidPin))
                .toString();

        OperationsProfileRegistry.State parsed = OperationsProfileRegistry.parse(serialized);
        assertEquals(1, parsed.profiles.size());
        assertEquals("host_1", parsed.activeHostId);
        assertFalse(parsed.active().revoked);
    }

    @Test
    public void registryHasABoundedComputerCount() {
        OperationsProfileRegistry.State state = OperationsProfileRegistry.empty();
        for (int index = 1; index <= OperationsProfileRegistry.MAX_PROFILES; index++) {
            state = state.upsert(
                    "https://192.168.1." + (20 + index) + ":5800",
                    PIN,
                    "host_" + index);
        }
        assertEquals(OperationsProfileRegistry.MAX_PROFILES, state.profiles.size());
        OperationsProfileRegistry.State full = state;
        assertThrows(IllegalStateException.class, () -> full.upsert(
                "https://192.168.1.99:5800", PIN, "host_7"));
    }

    @Test
    public void invalidRecentTaskIsClearedInsteadOfCrossingProfiles() {
        OperationsProfileRegistry.State state = OperationsProfileRegistry.empty()
                .upsert("https://192.168.1.21:5800", PIN, "host_1")
                .updateRelayTask("../../task", "arbitrary.command", "idem");

        assertEquals("", state.active().relayTaskId);
        assertEquals("", state.active().relayTaskCapability);
        assertEquals("", state.active().relayTaskIdempotency);
    }

    @Test
    public void localLabelsAreBoundedAndStayWithTheirComputer() {
        OperationsProfileRegistry.State state = OperationsProfileRegistry.empty()
                .upsert("https://192.168.1.21:5800", PIN, "host_1")
                .rename("host_1", "  一号线\nAOI-ABCDEFGHIJKLMNOPQRST  ")
                .upsert("https://192.168.1.22:5800", "b".repeat(64), "host_2")
                .rename("host_2", "实验室");

        assertEquals("实验室", state.active().label);
        assertEquals("实验室", state.activeDisplayLabel());
        String firstLabel = state.select("host_1").active().label;
        assertFalse(firstLabel.contains("\n"));
        assertTrue(firstLabel.length() <= 20);
        assertEquals(firstLabel, OperationsProfileRegistry.parse(
                OperationsProfileRegistry.serialize(state)).select("host_1").active().label);
    }

    @Test
    public void unnamedComputersHaveStableLocalDisplayNames() {
        OperationsProfileRegistry.State state = OperationsProfileRegistry.empty()
                .upsert("https://192.168.1.21:5800", PIN, "host_1")
                .upsert("https://192.168.1.22:5800", "b".repeat(64), "host_2");

        assertEquals("电脑 2", state.activeDisplayLabel());
        assertEquals("电脑 1", state.displayLabel("host_1"));
        assertEquals("未选择", state.displayLabel("missing"));
    }

    private static JSONObject profile(String hostId, String endpoint, String pin) throws Exception {
        return new JSONObject()
                .put("hostId", hostId)
                .put("endpoint", endpoint)
                .put("certificatePin", pin)
                .put("connectionPreference", OperationsConnectionPreference.DIRECT)
                .put("revoked", false)
                .put("watchHistory", "")
                .put("relayTaskId", "")
                .put("relayTaskCapability", "")
                .put("relayTaskIdempotency", "");
    }
}
