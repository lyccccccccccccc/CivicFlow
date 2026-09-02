import { useState } from 'react'
import { Alert, Button, Stack, Typography } from '@mui/material'
import { CircleMarker, MapContainer, TileLayer, useMapEvents } from 'react-leaflet'

const tileUrl = import.meta.env.VITE_MAP_TILE_URL || 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png'
const attribution = import.meta.env.VITE_MAP_ATTRIBUTION || '&copy; OpenStreetMap contributors'
const defaultCentre: [number, number] = [-27.4698, 153.0251]

export type MapPoint = { latitude: number; longitude: number }

export function MapPicker({ value, onChange }: { value?: MapPoint; onChange: (value?: MapPoint) => void }) {
  const [unavailable, setUnavailable] = useState(false)
  return <Stack spacing={1}>
    <Typography sx={{ fontWeight: 700 }}>Pin the location (optional)</Typography>
    <Typography variant="body2" color="text.secondary">Click the map to place or move the marker. The address above remains required.</Typography>
    {unavailable && <Alert severity="warning">Map tiles are unavailable. You can still submit the request using the written address.</Alert>}
    <MapContainer center={value ? [value.latitude, value.longitude] : defaultCentre} zoom={value ? 16 : 11} style={{ height: 'clamp(220px, 45vw, 340px)', width: '100%', borderRadius: 8 }}>
      <TileLayer url={tileUrl} attribution={attribution} eventHandlers={{ tileerror: () => setUnavailable(true) }} />
      <MapClick onChange={onChange} />
      {value && <CircleMarker center={[value.latitude, value.longitude]} radius={9} pathOptions={{ color: '#b42318', fillOpacity: 0.85 }} />}
    </MapContainer>
    {value && <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center' }}><Typography variant="caption">{value.latitude.toFixed(6)}, {value.longitude.toFixed(6)}</Typography><Button size="small" onClick={() => onChange(undefined)}>Remove pin</Button></Stack>}
  </Stack>
}

function MapClick({ onChange }: { onChange: (value: MapPoint) => void }) {
  useMapEvents({ click: event => onChange({ latitude: event.latlng.lat, longitude: event.latlng.lng }) })
  return null
}
