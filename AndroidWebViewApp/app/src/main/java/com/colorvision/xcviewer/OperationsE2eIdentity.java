package com.colorvision.xcviewer;

import android.os.Build;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;

import java.security.KeyPair;
import java.security.KeyPairGenerator;
import java.security.KeyStore;
import java.security.PrivateKey;
import java.security.spec.ECGenParameterSpec;

import javax.crypto.KeyAgreement;

final class OperationsE2eIdentity {
    private static final String KEYSTORE = "AndroidKeyStore";
    private static final String ALIAS_PREFIX = "colorvision_operations_e2e_";

    private final String alias;

    OperationsE2eIdentity(String hostId) {
        if (!OperationsRelayPolicy.isSafeIdentifier(hostId)) {
            throw new IllegalArgumentException("invalid_e2e_host_id");
        }
        alias = ALIAS_PREFIX + hostId;
    }

    static boolean isSupported() {
        return Build.VERSION.SDK_INT >= OperationsRemoteWindowSnapshot.MINIMUM_ANDROID_SDK;
    }

    String getPublicKeySpki() throws Exception {
        return OperationsRemoteWindowSnapshot.canonicalPublicKey(ensureKeyPair().getPublic());
    }

    byte[] deriveSharedSecret(String peerPublicKeySpki) throws Exception {
        KeyPair pair = ensureKeyPair();
        KeyAgreement agreement = KeyAgreement.getInstance("ECDH", KEYSTORE);
        agreement.init(pair.getPrivate());
        agreement.doPhase(
                OperationsRemoteWindowSnapshot.parseP256PublicKey(peerPublicKeySpki), true);
        return agreement.generateSecret();
    }

    void delete() throws Exception {
        KeyStore store = KeyStore.getInstance(KEYSTORE);
        store.load(null);
        if (store.containsAlias(alias)) {
            store.deleteEntry(alias);
        }
    }

    private KeyPair ensureKeyPair() throws Exception {
        if (!isSupported()) {
            throw new UnsupportedOperationException("window_snapshot_e2e_requires_android_31");
        }
        KeyStore store = KeyStore.getInstance(KEYSTORE);
        store.load(null);
        KeyStore.Entry entry = store.getEntry(alias, null);
        if (entry instanceof KeyStore.PrivateKeyEntry) {
            KeyStore.PrivateKeyEntry privateEntry = (KeyStore.PrivateKeyEntry) entry;
            OperationsRemoteWindowSnapshot.canonicalPublicKey(
                    privateEntry.getCertificate().getPublicKey());
            PrivateKey privateKey = privateEntry.getPrivateKey();
            return new KeyPair(privateEntry.getCertificate().getPublicKey(), privateKey);
        }
        if (entry != null || store.containsAlias(alias)) {
            throw new SecurityException("invalid_window_snapshot_e2e_key");
        }

        KeyPairGenerator generator = KeyPairGenerator.getInstance(
                KeyProperties.KEY_ALGORITHM_EC, KEYSTORE);
        KeyGenParameterSpec spec = new KeyGenParameterSpec.Builder(
                alias, KeyProperties.PURPOSE_AGREE_KEY)
                .setAlgorithmParameterSpec(new ECGenParameterSpec("secp256r1"))
                .setUserAuthenticationRequired(false)
                .build();
        generator.initialize(spec);
        return generator.generateKeyPair();
    }
}
