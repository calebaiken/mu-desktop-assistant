# Application boundary

Implements ISettingsStore and IEventLog ports plus typed load/event results. References
Core only. Future use cases include conversation orchestration, primary assistant,
planner, validated tool execution, consent, scoped memory and voice coordination.
Model plans are proposals, never authority. Outer adapters implement these ports.
