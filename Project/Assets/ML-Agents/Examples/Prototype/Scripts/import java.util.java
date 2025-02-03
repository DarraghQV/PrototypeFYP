import java.util;

public class CaesarCipher {

    // Method to encrypt text using Caesar Cipher
    public static String encrypt(String plaintext, int shift) {
        StringBuilder ciphertext = new StringBuilder();

        for (int i = 0; i < plaintext.length(); i++) {
            char ch = plaintext.charAt(i);

            if (Character.isLetter(ch)) {
                char base = Character.isLowerCase(ch) ? 'a' : 'A';
                ch = (char) ((ch - base + shift + 26) % 26 + base);
            }

            ciphertext.append(ch);
        }

        return ciphertext.toString();
    }

    // Method to decrypt text using Caesar Cipher
    public static String decrypt(String ciphertext, int shift) {
        return encrypt(ciphertext, -shift);
    }

    public static void main(String[] args) {
        Scanner scanner = new Scanner(System.in);

        System.out.println("Caesar Cipher Implementation");

        // Get plaintext from user
        System.out.print("Enter plaintext: ");
        String plaintext = scanner.nextLine();

        // Get shift value from user
        System.out.print("Enter shift value (positive for right shift, negative for left shift): ");
        int shift = scanner.nextInt();

        // Encrypt the plaintext
        String encryptedText = encrypt(plaintext, shift);
        System.out.println("Encrypted text: " + encryptedText);

        // Decrypt the ciphertext
        String decryptedText = decrypt(encryptedText, shift);
        System.out.println("Decrypted text: " + decryptedText);

        scanner.close();
    }
}
