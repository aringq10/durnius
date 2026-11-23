import { HubConnectionState } from "../lib/protocol";

const LABELS: Record<HubConnectionState, string> = {
  [HubConnectionState.Connected]: "Online",
  [HubConnectionState.Connecting]: "Connecting…",
  [HubConnectionState.Reconnecting]: "Reconnecting…",
  [HubConnectionState.Disconnecting]: "Offline",
  [HubConnectionState.Disconnected]: "Offline",
};

/** Small live-status pill for the SignalR connection. */
export default function ConnectionDot({
  state,
}: {
  state: HubConnectionState;
}) {
  const tone =
    state === HubConnectionState.Connected
      ? "conn-on"
      : state === HubConnectionState.Disconnected ||
          state === HubConnectionState.Disconnecting
        ? "conn-off"
        : "conn-busy";
  return (
    <span className={`conn ${tone}`} role="status">
      <span className="conn-dot" aria-hidden="true" />
      {LABELS[state]}
    </span>
  );
}
