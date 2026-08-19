package com.colorvision.xcviewer;

import android.os.Build;
import android.security.keystore.KeyInfo;
import android.security.keystore.KeyProperties;

import androidx.test.ext.junit.runners.AndroidJUnit4;

import org.junit.Test;
import org.junit.runner.RunWith;

import java.security.KeyFactory;
import java.security.KeyPair;
import java.security.KeyPairGenerator;
import java.security.KeyStore;
import java.security.PrivateKey;
import java.security.spec.ECGenParameterSpec;
import java.util.Arrays;

import javax.crypto.KeyAgreement;

import static org.junit.Assert.assertArrayEquals;
import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

@RunWith(AndroidJUnit4.class)
public final class OperationsE2eIdentityInstrumentedTest {
    @Test
    public void androidKeyStoreUsesASeparateAgreeOnlyP256Identity() throws Exception {
        assertTrue("The connected validation device must run Android 12 or newer",
                Build.VERSION.SDK_INT >= 31);
        String hostId = "instrumented_e2e_identity";
        String e2eAlias = "colorvision_operations_e2e_" + hostId;
        String signingAlias = "colorvision_operations_" + hostId;
        OperationsE2eIdentity e2e = new OperationsE2eIdentity(hostId);
        OperationsDeviceIdentity signing = new OperationsDeviceIdentity(hostId);
        KeyStore store = KeyStore.getInstance("AndroidKeyStore");
        store.load(null);
        try {
            e2e.delete();
            signing.delete();
            String signingSpkiBefore = signing.getPublicKeySpki();
            String e2eSpki = e2e.getPublicKeySpki();
            assertEquals(e2eSpki, e2e.getPublicKeySpki());
            assertFalse(signingSpkiBefore.equals(e2eSpki));

            PrivateKey e2ePrivate = (PrivateKey) store.getKey(e2eAlias, null);
            KeyInfo keyInfo = KeyFactory.getInstance("EC", "AndroidKeyStore")
                    .getKeySpec(e2ePrivate, KeyInfo.class);
            assertEquals(KeyProperties.PURPOSE_AGREE_KEY, keyInfo.getPurposes());
            assertEquals(256, keyInfo.getKeySize());

            KeyPairGenerator peerGenerator = KeyPairGenerator.getInstance("EC");
            peerGenerator.initialize(new ECGenParameterSpec("secp256r1"));
            KeyPair peer = peerGenerator.generateKeyPair();
            String peerSpki = OperationsRemoteWindowSnapshot.canonicalPublicKey(
                    peer.getPublic());
            byte[] fromAndroidKeyStore = e2e.deriveSharedSecret(peerSpki);
            KeyAgreement peerAgreement = KeyAgreement.getInstance("ECDH");
            peerAgreement.init(peer.getPrivate());
            peerAgreement.doPhase(
                    OperationsRemoteWindowSnapshot.parseP256PublicKey(e2eSpki), true);
            byte[] fromPeer = peerAgreement.generateSecret();
            assertEquals(32, fromAndroidKeyStore.length);
            assertArrayEquals(fromPeer, fromAndroidKeyStore);
            Arrays.fill(fromAndroidKeyStore, (byte) 0);
            Arrays.fill(fromPeer, (byte) 0);

            e2e.delete();
            assertFalse(store.containsAlias(e2eAlias));
            assertTrue(store.containsAlias(signingAlias));
            assertEquals(signingSpkiBefore, signing.getPublicKeySpki());
        } finally {
            e2e.delete();
            signing.delete();
        }
    }
}
