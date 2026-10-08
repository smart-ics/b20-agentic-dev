import { describe, expect, it } from 'vitest'
import {
  normalizeTaskLine,
  parseCandidateTaskLine,
  parseTaskListTasks,
  parseTaskListText,
} from '@/api/requests'

describe('parseCandidateTaskLine', () => {
  describe('Trailing points expressions', () => {
    it.each([
      ['Implement authentication 1pts', 'Implement authentication', 1],
      ['Implement authentication 1 pts', 'Implement authentication', 1],
      ['Configure redis cache 2 pt', 'Configure redis cache', 2],
      ['Configure redis cache 2 pts', 'Configure redis cache', 2],
      ['Design database schema 3 point', 'Design database schema', 3],
      ['Design database schema 3 points', 'Design database schema', 3],
      ['Refactor billing service 4 pts', 'Refactor billing service', 4],
      ['Build telemetry dashboard 5 points', 'Build telemetry dashboard', 5],
    ])('should parse "%s" -> title: "%s", complexity: %i', (input, expectedTitle, expectedComplexity) => {
      const result = parseCandidateTaskLine(input)
      expect(result).not.toBeNull()
      expect(result).toEqual({
        title: expectedTitle,
        complexity: expectedComplexity,
      })
    })
  })

  describe('Case insensitivity', () => {
    it.each([
      ['Task alpha 2 PTS', 'Task alpha', 2],
      ['Task beta 3 Pts', 'Task beta', 3],
      ['Task gamma 1 Point', 'Task gamma', 1],
      ['Task delta 4 POINTS', 'Task delta', 4],
    ])('should parse case variations "%s" -> title: "%s", complexity: %i', (input, expectedTitle, expectedComplexity) => {
      const result = parseCandidateTaskLine(input)
      expect(result).toEqual({
        title: expectedTitle,
        complexity: expectedComplexity,
      })
    })
  })

  describe('Bracketed and parenthesized expressions', () => {
    it.each([
      ['Setup notification worker [3 pts]', 'Setup notification worker', 3],
      ['Migrate customer data (2 points)', 'Migrate customer data', 2],
      ['Audit logging handler {4 pt}', 'Audit logging handler', 4],
      ['Container health probe [5 points]', 'Container health probe', 5],
      ['API rate limiter (1 pt)', 'API rate limiter', 1],
    ])('should parse bracketed notation "%s" -> title: "%s", complexity: %i', (input, expectedTitle, expectedComplexity) => {
      const result = parseCandidateTaskLine(input)
      expect(result).toEqual({
        title: expectedTitle,
        complexity: expectedComplexity,
      })
    })
  })

  describe('Embedded numbers in titles that should NOT match as complexity', () => {
    it('preserves embedded number when trailing points expression is present', () => {
      const input = 'Allow user re-login for 3 consecutive failed 3 points'
      const result = parseCandidateTaskLine(input)
      expect(result).toEqual({
        title: 'Allow user re-login for 3 consecutive failed',
        complexity: 3,
      })
    })

    it('preserves years and embedded digits when trailing points expression is present', () => {
      const input = 'Delete user which has been dormant for 1 years 2 pts'
      const result = parseCandidateTaskLine(input)
      expect(result).toEqual({
        title: 'Delete user which has been dormant for 1 years',
        complexity: 2,
      })
    })

    it('preserves embedded number when no trailing points expression exists', () => {
      const input = 'Allow user re-login for 3 consecutive failed logins'
      const result = parseCandidateTaskLine(input)
      expect(result).toEqual({
        title: 'Allow user re-login for 3 consecutive failed logins',
        complexity: 1,
      })
    })
  })

  describe('Clean title extraction', () => {
    it('strips points expression and trims trailing whitespace', () => {
      const input = '  Fix navbar dropdown padding   4 pts   '
      const result = parseCandidateTaskLine(input)
      expect(result).toEqual({
        title: 'Fix navbar dropdown padding',
        complexity: 4,
      })
    })

    it('returns null when the entire line is empty or only whitespace', () => {
      expect(parseCandidateTaskLine('')).toBeNull()
      expect(parseCandidateTaskLine('   ')).toBeNull()
      expect(parseCandidateTaskLine('\t\n')).toBeNull()
    })

    it('returns null when the line contains only a complexity tag and no title', () => {
      expect(parseCandidateTaskLine('3 pts')).toBeNull()
      expect(parseCandidateTaskLine('[2 points]')).toBeNull()
      expect(parseCandidateTaskLine('- 1 pt')).toBeNull()
    })
  })

  describe('Default fallback', () => {
    it('defaults complexity to 1 when no points expression exists', () => {
      const input = 'Fix header alignment'
      const result = parseCandidateTaskLine(input)
      expect(result).toEqual({
        title: 'Fix header alignment',
        complexity: 1,
      })
    })
  })

  describe('Out-of-range numbers (1..5 only)', () => {
    it('retains "8 pts" in title and defaults complexity to 1', () => {
      const input = 'Implement feature X 8 pts'
      const result = parseCandidateTaskLine(input)
      expect(result).toEqual({
        title: 'Implement feature X 8 pts',
        complexity: 1,
      })
    })

    it('retains "0 pts" in title and defaults complexity to 1', () => {
      const input = 'Review code 0 pts'
      const result = parseCandidateTaskLine(input)
      expect(result).toEqual({
        title: 'Review code 0 pts',
        complexity: 1,
      })
    })

    it('retains bracketed out-of-range "[9 points]" in title and defaults complexity to 1', () => {
      const input = 'Complex migration [9 points]'
      const result = parseCandidateTaskLine(input)
      expect(result).toEqual({
        title: 'Complex migration [9 points]',
        complexity: 1,
      })
    })
  })

  describe('Prefix normalization', () => {
    it.each([
      ['- Bullet item 2 pts', 'Bullet item', 2],
      ['1. Numbered item 3 pts', 'Numbered item', 3],
      ['* Star item 4 pts', 'Star item', 4],
      ['[ ] Checkbox item 5 pts', 'Checkbox item', 5],
      ['[x] Checked checkbox 1 pt', 'Checked checkbox', 1],
      ['[X] Uppercase checked checkbox 2 pts', 'Uppercase checked checkbox', 2],
      ['+ Plus bullet item 3 pts', 'Plus bullet item', 3],
      ['(1) Parenthesized numbering 4 pts', 'Parenthesized numbering', 4],
      ['1) Closing parenthesis numbering 5 pts', 'Closing parenthesis numbering', 5],
    ])('should normalize prefix for "%s" -> title: "%s", complexity: %i', (input, expectedTitle, expectedComplexity) => {
      const result = parseCandidateTaskLine(input)
      expect(result).toEqual({
        title: expectedTitle,
        complexity: expectedComplexity,
      })
    })
  })

  describe('Character length capping', () => {
    it('caps title at 255 characters', () => {
      const longTitle = 'a'.repeat(300)
      const input = `${longTitle} 3 pts`
      const result = parseCandidateTaskLine(input)
      expect(result).not.toBeNull()
      expect(result?.title.length).toBe(255)
      expect(result?.title).toBe('a'.repeat(255))
      expect(result?.complexity).toBe(3)
    })
  })
})

describe('normalizeTaskLine', () => {
  it('strips markdown checkboxes and bullets', () => {
    expect(normalizeTaskLine('[ ] Deploy to staging')).toBe('Deploy to staging')
    expect(normalizeTaskLine('[x] Verified build')).toBe('Verified build')
    expect(normalizeTaskLine('- Task item')).toBe('Task item')
    expect(normalizeTaskLine('1. First item')).toBe('First item')
  })
})

describe('parseTaskListTasks', () => {
  it('parses multiline input with empty lines, trimming, and complexity detection', () => {
    const rawText = `
      - User registration flow 3 pts
      
      [ ] Password reset endpoint 2 pt
      
      * Add telemetry events (5 points)
      Direct title without points
      8 pts out-of-range task 8 pts
      
    `
    const tasks = parseTaskListTasks(rawText)

    expect(tasks).toEqual([
      { title: 'User registration flow', complexity: 3 },
      { title: 'Password reset endpoint', complexity: 2 },
      { title: 'Add telemetry events', complexity: 5 },
      { title: 'Direct title without points', complexity: 1 },
      { title: '8 pts out-of-range task 8 pts', complexity: 1 },
    ])
  })

  it('returns an empty array when input is empty or contains only whitespace', () => {
    expect(parseTaskListTasks('')).toEqual([])
    expect(parseTaskListTasks('   \n  \n\t  ')).toEqual([])
  })

  it('caps each title at 255 characters across multiline entries', () => {
    const longLine1 = `1. ${'x'.repeat(280)} 4 pts`
    const longLine2 = `2. ${'y'.repeat(280)}`
    const tasks = parseTaskListTasks(`${longLine1}\n${longLine2}`)

    expect(tasks).toHaveLength(2)
    expect(tasks[0].title.length).toBe(255)
    expect(tasks[0].title).toBe('x'.repeat(255))
    expect(tasks[0].complexity).toBe(4)

    expect(tasks[1].title.length).toBe(255)
    expect(tasks[1].title).toBe('y'.repeat(255))
    expect(tasks[1].complexity).toBe(1)
  })
})

describe('parseTaskListText (Backward Compatibility)', () => {
  it('returns an array of title strings from multiline input', () => {
    const rawText = `
      - Task one 3 pts
      - Task two 1 pt
      Task three
    `
    const titles = parseTaskListText(rawText)

    expect(titles).toEqual(['Task one', 'Task two', 'Task three'])
  })

  it('returns an empty array for empty input', () => {
    expect(parseTaskListText('')).toEqual([])
  })
})
