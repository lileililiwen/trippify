import 'package:flutter/material.dart';

import '../api_client.dart';
import '../l10n/generated/app_localizations.dart';

/// Confirmation screen shown after a successful registration. Lists the
/// next steps (check inbox, check spam, request resend) and surfaces a
/// "Resend verification" action.
class RegistrationConfirmationScreen extends StatefulWidget {
  const RegistrationConfirmationScreen({super.key, required this.api, this.email});
  final AppApi api;
  final String? email;

  @override
  State<RegistrationConfirmationScreen> createState() => _RegistrationConfirmationScreenState();
}

class _RegistrationConfirmationScreenState extends State<RegistrationConfirmationScreen> {
  String? status;
  bool busy = false;

  Future<void> _resend() async {
    setState(() {
      busy = true;
      status = null;
    });
    try {
      await widget.api.resendVerification();
      if (!mounted) return;
      setState(() {
        status = 'resendSuccess';
        busy = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        status = 'resendFailure';
        busy = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    return Scaffold(
    appBar: AppBar(title: Text(l10n.registerSuccessTitle)),
    body: ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Icon(
          Icons.mark_email_read_outlined,
          size: 64,
          color: Theme.of(context).colorScheme.primary,
        ),
        const SizedBox(height: 16),
        Text(
          widget.email == null
              ? l10n.registerSuccessBodyNoEmail
              : l10n.registerSuccessBodyWithEmail(widget.email!),
          style: Theme.of(context).textTheme.titleMedium,
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 24),
        _checklistItem(context, l10n.registerChecklistInbox),
        _checklistItem(context, l10n.registerChecklistSpam),
        _checklistItem(context, l10n.registerChecklistClickLink),
        const SizedBox(height: 16),
        if (status != null)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 8),
            child: Semantics(
              liveRegion: true,
              child: Text(
                status == 'resendSuccess'
                    ? l10n.registerResentSuccess
                    : status == 'resendFailure'
                        ? l10n.registerResentFailure
                        : status!,
                textAlign: TextAlign.center,
              ),
            ),
          ),
        FilledButton(
          onPressed: busy ? null : _resend,
          child: busy
              ? const SizedBox(
                  width: 16,
                  height: 16,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : Text(l10n.registerResend),
        ),
        const SizedBox(height: 12),
        OutlinedButton(
          onPressed: busy
              ? null
              : () {
                  Navigator.popUntil(context, (r) => r.isFirst);
                  Navigator.pushReplacementNamed(context, '/sign-in');
                },
          child: Text(l10n.registerBackToSignIn),
        ),
      ],
    ),
  );
  }

  Widget _checklistItem(BuildContext context, String text) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 4),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(Icons.check, color: Theme.of(context).colorScheme.primary),
        const SizedBox(width: 12),
        Expanded(child: Text(text)),
      ],
    ),
  );
}
