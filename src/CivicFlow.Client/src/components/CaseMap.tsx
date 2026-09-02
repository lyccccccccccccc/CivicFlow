import { useState } from 'react'
import { Alert, Stack, Typography } from '@mui/material'
import { CircleMarker, MapContainer, TileLayer } from 'react-leaflet'

const tileUrl = import.meta.env.VITE_MAP_TILE_URL ?? 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png'
const attribution = import.meta.env.VITE_MAP_ATTRIBUTION ?? '&copy; OpenStreetMap contributors'

export function CaseMap({ latitude, longitude }: { latitude?: number; longitude?: number }) {
  const [unavailable, setUnavailable] = useState(false)
  if (latitude == null || longitude == null) return null
  return <Stack spacing={1} sx={{ mt: 2 }}>
    <Typography sx={{ fontWeight: 700 }}>Map location</Typography>
    {unavailable && <Alert severity="warning">Map tiles are currently unavailable. Coordinates remain recorded below.</Alert>}
    <MapContainer center={[latitude, longitude]} zoom={16} dragging={false} scrollWheelZoom={false} doubleClickZoom={false} style={{ height: 'clamp(210px, 38vw, 280px)', width: '100%', borderRadius: 8 }}>
      <TileLayer url={tileUrl} attribution={attribution} eventHandlers={{ tileerror: () => setUnavailable(true) }} />
      <CircleMarker center={[latitude, longitude]} radius={9} pathOptions={{ color: '#b42318', fillOpacity: 0.85 }} />
    </MapContainer>
    <Typography variant="caption" color="text.secondary">{latitude.toFixed(6)}, {longitude.toFixed(6)}</Typography>
  </Stack>
}
