#ifndef I2C_H
#define I2C_H

#include <avr/io.h>
#include <util/delay.h>

#define DDR_USI       DDRB
#define PORT_USI      PORTB
#define PIN_USI       PINB
#define PORT_USI_SDA  PORTB0
#define PORT_USI_SCL  PORTB2
#define PIN_USI_SDA   PINB0
#define PIN_USI_SCL   PINB2
#define DDR_USI_CL    DDR_USI
#define PORT_USI_CL   PORT_USI
#define PIN_USI_CL    PIN_USI

#define OVERCLOCK
//#define TWI_FAST_MODE

#ifdef OVERCLOCK
#define DELAY_T2TWI ;
#define DELAY_T4TWI ;
#elif defined(TWI_FAST_MODE)         // TWI FAST mode timing limits. SCL = 100-400kHz
#define DELAY_T2TWI (_delay_us(2))   // >1.3us
#define DELAY_T4TWI (_delay_us(1))   // >0.6us
#else                                // TWI STANDARD mode timing limits. SCL <= 100kHz
#define DELAY_T2TWI (_delay_us(5))   // >4.7us
#define DELAY_T4TWI (_delay_us(4))   // >4.0us
#endif

#define TWI_NACK_BIT 0 // Bit position for (N)ACK bit.

// Constants
// Prepare register value to: Clear flags, and set USI to shift 8 bits i.e. count 16 clock edges.
#define USISR_8bit (1<<USISIF | 1<<USIOIF | 1<<USIPF | 1<<USIDC | 0x0<<USICNT0)
// Prepare register value to: Clear flags, and set USI to shift 1 bit i.e. count 2 clock edges.
#define USISR_1bit (1<<USISIF | 1<<USIOIF | 1<<USIPF | 1<<USIDC | 0xE<<USICNT0)

#ifndef uchar
#define uchar   unsigned char
#endif

void i2c_init(void);
void i2c_start(void);
void i2c_repstart(void);
void i2c_stop(void);
uchar i2c_put_u08(uchar data);
uchar i2c_get_u08(uchar last);

#endif