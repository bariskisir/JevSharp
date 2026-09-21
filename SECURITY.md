# Security policy

Security fixes target the latest published version. Upgrade to the latest release before reporting a suspected issue.

Please report vulnerabilities privately using [GitHub private vulnerability reporting](https://github.com/bariskisir/JevSharp/security/advisories/new). If private reporting is unavailable, open an issue requesting a private contact channel without including exploit details or credentials.

Include the affected version, reproduction steps, impact, and a minimal sanitized example. Do not include API keys, authentication headers, or customer data. Rotate any credentials that were exposed.

Applications own endpoint trust, credential storage, and business decisions based on model probabilities. Use HTTPS for remote endpoints. Keep keys in a secret store or protected configuration. Error `Details` may contain application data even after known header credentials have been redacted.
