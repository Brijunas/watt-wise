import { describe, expect, it } from 'vitest'
import html from '../../index.html?raw'

const socialTags = (source: string) =>
  (source.match(/<meta\s+(?:property|name)="(?:og|twitter):[^>]*>/g) ?? []).map((tag) =>
    tag.replace(/\s+/g, ' '),
  )

describe('frontend index.html social tags', () => {
  it('has exactly the Open Graph and Twitter set', () => {
    expect(socialTags(html)).toEqual([
      '<meta property="og:type" content="website" />',
      '<meta property="og:site_name" content="Watt-Wise" />',
      '<meta property="og:title" content="Watt-Wise" />',
      '<meta property="og:description" content="Supraskite ir sumažinkite elektros sąskaitas pagal valandines kainas ir išmaniuosius planus." />',
      '<meta property="og:url" content="${APP_URL}/" />',
      '<meta property="og:locale" content="lt_LT" />',
      '<meta name="twitter:card" content="summary" />',
    ])
  })
})

describe('frontend index.html theme-color tags', () => {
  it('has exactly two, one per colour scheme', () => {
    const tags = html.match(/<meta\s+name="theme-color"[^>]*>/g) ?? []
    expect(tags.map((tag) => tag.replace(/\s+/g, ' '))).toEqual([
      '<meta name="theme-color" media="(prefers-color-scheme: light)" content="#F7F7FC" />',
      '<meta name="theme-color" media="(prefers-color-scheme: dark)" content="#05060E" />',
    ])
  })
})
