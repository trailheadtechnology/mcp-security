#!/bin/sh
# Opens the Entra admin center pages shown during the demos, in Chrome. See DEMO.md.
# If Microsoft asks which account, pick the tenant admin (not alice or bob).
APP=07f59dcc-1a3f-456f-94a9-f79da75c8326
SP=afcbc311-db2f-4f0f-831c-b585df0084e9
CONTOSO=917e4b14-22b2-4536-a3be-b36af92a76a6
FABRIKAM=0b454f2d-beb9-4168-94b9-0dccf8f6a342
E=https://entra.microsoft.com/#view

open -na "Google Chrome" --args --new-window \
  "$E/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/AppRoles/appId/$APP/isMSAApp~/false" \
  "$E/Microsoft_AAD_IAM/ManagedAppMenuBlade/~/Users/objectId/$SP/appId/$APP" \
  "$E/Microsoft_AAD_IAM/GroupDetailsMenuBlade/~/Members/groupId/$CONTOSO" \
  "$E/Microsoft_AAD_IAM/GroupDetailsMenuBlade/~/Members/groupId/$FABRIKAM"
