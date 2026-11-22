import { describe, expect, it } from 'vitest'
import html from '../../index.html?raw'

describe('admin index.html theme-color tags', () => {
  it('has exactly two, one per colour scheme', () => {
    const tags = html.match(/<meta\s+name="theme-color"[^>]*>/g) ?? []
    expect(tags.map((tag) => tag.replace(/\s+/g, ' '))).toEqual([
      '<meta name="theme-color" media="(prefers-color-scheme: light)" content="#F7F7FC" />',
      '<meta name="theme-color" media="(prefers-color-scheme: dark)" content="#05060E" />',
    ])
  })
})
