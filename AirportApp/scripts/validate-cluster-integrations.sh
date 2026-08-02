#!/bin/sh
set -eu

google_client_id="$(cat /run/secrets/airportapp_google_client_id_v2)"
google_client_secret="$(cat /run/secrets/airportapp_google_client_secret_v2)"
google_response="$(
    curl --silent --show-error --max-time 30 \
        --request POST \
        --data-urlencode "client_id=${google_client_id}" \
        --data-urlencode "client_secret=${google_client_secret}" \
        --data-urlencode "code=airportapp-intentional-invalid-code" \
        --data-urlencode "grant_type=authorization_code" \
        --data-urlencode "redirect_uri=http://localhost:5164/signin-google" \
        https://oauth2.googleapis.com/token
)"

if printf '%s' "$google_response" |
    grep -Eq '"error"[[:space:]]*:[[:space:]]*"invalid_grant"'; then
    echo "GOOGLE_OAUTH_CREDENTIALS=VALID"
else
    echo "GOOGLE_OAUTH_CREDENTIALS=INVALID"
    exit 1
fi

paypal_client_id="$(cat /run/secrets/airportapp_paypal_client_id_v2)"
paypal_client_secret="$(cat /run/secrets/airportapp_paypal_client_secret_v2)"
paypal_response="$(
    curl --silent --show-error --max-time 30 \
        --user "${paypal_client_id}:${paypal_client_secret}" \
        --header "Accept: application/json" \
        --data "grant_type=client_credentials" \
        https://api-m.sandbox.paypal.com/v1/oauth2/token
)"

if printf '%s' "$paypal_response" | grep -q '"access_token"'; then
    echo "PAYPAL_SANDBOX_CREDENTIALS=VALID"
else
    echo "PAYPAL_SANDBOX_CREDENTIALS=INVALID"
    exit 1
fi

email_password="$(cat /run/secrets/airportapp_email_password_v2)"
curl --silent --show-error --max-time 30 \
    --url "smtp://smtp.gmail.com:587" \
    --ssl-reqd \
    --user "snreyes2@espe.edu.ec:${email_password}" \
    --request "NOOP" \
    --output /dev/null
echo "SMTP_CREDENTIALS=VALID"
